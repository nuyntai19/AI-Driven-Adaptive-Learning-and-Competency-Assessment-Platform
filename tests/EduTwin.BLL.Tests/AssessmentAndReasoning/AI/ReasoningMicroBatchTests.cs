using System.Collections.Concurrent;
using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class ReasoningMicroBatchTests
{
    [Theory]
    [InlineData(50, 3, 17)]
    [InlineData(100, 5, 20)]
    public async Task ManyQuestionsKeepSeparateResultsWithFewerCalls(int count, int size, int expectedCalls)
    {
        var executor = new FakeExecutor();
        var clock = new ManualBatchClock();
        using var batcher = Create(executor, size, clock);
        var partition = Partition();
        var requests = Enumerable.Range(0, count).Select(i => Request(partition, i.ToString())).ToArray();
        var pending = requests.Select(r => batcher.AnalyzeAsync(r, default, partition)).ToArray();
        clock.Advance(TimeSpan.FromMilliseconds(20));
        var responses = await Task.WhenAll(pending);
        Assert.Equal(expectedCalls, executor.Groups.Count);
        Assert.Equal(count, responses.Length);
        Assert.Equal(requests.Select(r => r.StudentSubmission.FinalAnswer), responses.Select(r => r.Feedback));
        Assert.All(executor.Groups, g => Assert.InRange(g.Length, 1, size));
    }

    [Fact]
    public async Task PartialBatchFlushesAtItsConfiguredWindow_NotBefore()
    {
        var clock = new ManualBatchClock(); var executor = new FakeExecutor();
        using var batcher = Create(executor, clock: clock); var p = Partition();
        var pending = batcher.AnalyzeAsync(Request(p), default, p);
        clock.Advance(TimeSpan.FromMilliseconds(19));
        Assert.Empty(executor.Groups); Assert.False(pending.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("7", (await pending).Feedback);
        Assert.Single(executor.Groups);
    }

    [Fact]
    public async Task DoesNotMixCentersStudentsAssignmentsOrLanguages()
    {
        var executor = new FakeExecutor(); using var batcher = Create(executor);
        var p = Partition();
        var partitions = new[] { p, p with { CenterId = Guid.NewGuid() }, p with { StudentId = Guid.NewGuid() },
            p with { AssignmentId = Guid.NewGuid() }, p };
        await Task.WhenAll(partitions.Select((scope, i) => batcher.AnalyzeAsync(
            Request(null) with { Language = i == 4 ? "en" : "vi" }, default, scope)));
        Assert.Equal(5, executor.Groups.Count);
        Assert.All(executor.Groups, g => Assert.Single(g));
    }

    [Fact]
    public async Task ImagesAndInputSizeSplitGroupsWithoutDroppingEvidence()
    {
        var executor = new FakeExecutor(); using var batcher = Create(executor, 5);
        var p = Partition();
        var requests = Enumerable.Range(0, 3).Select(_ => Request(p) with
        {
            StudentSubmission = Request(p).StudentSubmission with { ImageParts = [new([1], "image/png"), new([2], "image/png")] }
        }).ToArray();
        await Task.WhenAll(requests.Select(r => batcher.AnalyzeAsync(r, default, p)));
        Assert.Equal(3, executor.Groups.Count);
        Assert.All(executor.Groups, g => Assert.Equal(2, g.Single().Request.StudentSubmission.ImageParts.Count));
    }

    [Fact]
    public async Task OneInvalidResultDoesNotDiscardValidResults()
    {
        var executor = new FakeExecutor { FailAnswer = "bad" }; using var batcher = Create(executor);
        var p = Partition();
        var good = batcher.AnalyzeAsync(Request(p, "good"), default, p);
        var bad = batcher.AnalyzeAsync(Request(p, "bad"), default, p);
        var other = batcher.AnalyzeAsync(Request(p, "other"), default, p);
        Assert.Equal("good", (await good).Feedback);
        await Assert.ThrowsAsync<InvalidOperationException>(() => bad);
        Assert.Equal("other", (await other).Feedback);
        Assert.Single(executor.Groups);
    }

    [Fact]
    public async Task ProblemAndScratchpadImages_BothCountTowardBatchBudget()
    {
        var executor = new FakeExecutor(); using var batcher = Create(executor, 5);
        var p = Partition(); var original = Request(p);
        var request = original with { Question = original.Question with { ImageParts = [new([1], "image/png")] },
            StudentSubmission = original.StudentSubmission with { ImageParts = [new([2], "image/png")] } };
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => batcher.AnalyzeAsync(request, default, p)));
        Assert.Equal(3, executor.Groups.Count);
        Assert.All(executor.Groups, g => Assert.Equal(2, g.Single().Request.AllImages().Count()));
    }

    [Fact]
    public async Task CancellationOfOneItemDoesNotCancelOtherItems()
    {
        var executor = new FakeExecutor(); using var batcher = Create(executor);
        using var cancel = new CancellationTokenSource(); var p = Partition();
        var canceled = batcher.AnalyzeAsync(Request(p, "cancel"), cancel.Token, p);
        cancel.Cancel();
        var good = batcher.AnalyzeAsync(Request(p, "good"), default, p);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.Equal("good", (await good).Feedback);
        Assert.DoesNotContain(executor.Groups.SelectMany(g => g), i => i.Request.StudentSubmission.FinalAnswer == "cancel");
    }

    [Fact]
    public async Task NonAssignmentRequestsDoNotCoalesce()
    {
        var executor = new FakeExecutor(); using var batcher = Create(executor);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => batcher.AnalyzeAsync(Request(null), default)));
        Assert.Equal(3, executor.Groups.Count);
    }

    [Fact]
    public async Task RepairExecutesOnlyTheFailedItemWithoutSplittingTheFreshBatch()
    {
        var executor = new FakeExecutor(); using var batcher = Create(executor);
        var p = Partition();
        var repair = batcher.AnalyzeAsync(Request(p, "repair") with { ResponseRepairRule = AIResponseValidationRule.Rubric }, default, p);
        var fresh = Enumerable.Range(0, 3).Select(i => batcher.AnalyzeAsync(Request(p, i.ToString()), default, p)).ToArray();
        await Task.WhenAll(fresh.Append(repair));
        Assert.Equal(2, executor.Groups.Count);
        Assert.Contains(executor.Groups, g => g.Length == 1 && g[0].Request.StudentSubmission.FinalAnswer == "repair");
        Assert.Contains(executor.Groups, g => g.Length == 3 && g.All(i => i.Request.ResponseRepairRule is null));
    }

    [Fact]
    public async Task TwoFullWavesOfJobsAvoidThreePlusOneSingletonBatches()
    {
        var executor = new FakeExecutor(); using var batcher = Create(executor);
        var p = Partition();
        for (var wave = 0; wave < 8; wave++)
            await Task.WhenAll(Enumerable.Range(wave * 6, 6).Select(i => batcher.AnalyzeAsync(Request(p, i.ToString()), default, p)));
        await Task.WhenAll(Enumerable.Range(48, 2).Select(i => batcher.AnalyzeAsync(Request(p, i.ToString()), default, p)));
        Assert.Equal(17, executor.Groups.Count);
        Assert.Equal(16, executor.Groups.Count(g => g.Length == 3));
        Assert.Single(executor.Groups, g => g.Length == 2);
        Assert.DoesNotContain(executor.Groups, g => g.Length == 1);
    }

    [Theory]
    [InlineData(3, 6, 24)]
    [InlineData(5, 10, 25)]
    public void ProductionDefaultsFeedWholeBatchesAndDoNotSplitAtTwentyFive(int batchSize, int jobs, int centerJobs)
    {
        var options = new AIGradingOptions { MicroBatchEnabled = true, BatchSize = batchSize };
        Assert.Equal(jobs, options.RecommendedJobConcurrency);
        Assert.Equal(centerJobs, options.RecommendedPerCenterJobs);
        Assert.Equal(0, jobs % batchSize);
        Assert.Equal(0, centerJobs % batchSize);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.BatchWindow);
    }

    [Fact]
    public async Task BatchExecutorMatchesIdsNotResponseOrderAndKeepsPerItemValidation()
    {
        var items = new[] { new ReasoningBatchItem("a", Request(null, "first")), new ReasoningBatchItem("b", Request(null, "second")) };
        var raw = JsonSerializer.Serialize(new { results = new[] {
            new { itemId = "b", analysis = ResponseJson("second") }, new { itemId = "a", analysis = ResponseJson("first") } } });
        var executor = Executor(raw);
        var result = await executor.ExecuteAsync(items, default);
        Assert.Equal("first", result["a"].Response!.Feedback);
        Assert.Equal("second", result["b"].Response!.Feedback);
        Assert.Equal("Gemini", executor.ProviderName);
        Assert.Equal("test-model", executor.ModelName);
        Assert.Contains("microbatch-v1", executor.ProfileVersion);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    public async Task UnknownOrDuplicateIdsRejectWholeAmbiguousEnvelope(string kind)
    {
        var raw = JsonSerializer.Serialize(new { results = new[] {
            new { itemId = "a", analysis = ResponseJson("one") }, new { itemId = kind == "unknown" ? "z" : "a", analysis = ResponseJson("two") } } });
        await Assert.ThrowsAsync<GeminiAdapterException>(() => Executor(raw).ExecuteAsync(
            [new("a", Request(null)), new("b", Request(null))], default));
    }

    [Fact]
    public async Task MissingOrInvalidItemPreservesOtherValidItem()
    {
        var raw = JsonSerializer.Serialize(new { results = new[] { new { itemId = "a", analysis = ResponseJson("one") } } });
        var results = await Executor(raw).ExecuteAsync([new("a", Request(null)), new("b", Request(null))], default);
        Assert.NotNull(results["a"].Response); Assert.NotNull(results["b"].Error);
    }

    [Fact]
    public void PromptMapsImagesAndDoesNotSendTenantIdentity()
    {
        var p = Partition(); var r = Request(p);
        r = r with { StudentSubmission = r.StudentSubmission with { ImageParts = [new([1], "image/png")] } };
        var prompt = new GeminiPromptBuilder().BuildBatch([new("a", r), new("b", r)]);
        Assert.Contains("\"imageIndexes\":[1]", prompt); Assert.Contains("\"imageIndexes\":[2]", prompt);
        Assert.DoesNotContain(p.CenterId.ToString(), prompt);
        Assert.Contains("Anti-Fallacy Safeguard", prompt); Assert.Contains("Method-Agnostic Evaluation", prompt);
    }

    [Fact]
    public void AlternativeModelRequiresExplicitApproval()
    {
        var gemini = new GeminiOptions { ApiKey = "fake", Model = "original" };
        var options = new AIGradingOptions { GeminiModel = "new-model" };
        Assert.Throws<GeminiAdapterException>(() => options.Validate(gemini));
        options.AlternativeProfileApproved = true; options.Validate(gemini);
    }

    internal static AIAnalysisBatchPartition Partition() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    internal static AnalyzeReasoningRequest Request(AIAnalysisBatchPartition? partition, string answer = "7") => new()
    {
        SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "vi",
        Question = new() { QuestionType = QuestionType.Essay, QuestionText = "Tính 2*3+1", CorrectAnswer = "7", Solution = "2*3+1=7",
            GradingCriteria = new() { SchemaVersion = "1", RequiredIdeas = [], CommonErrors = [], ScoringNotes = "Chấp nhận cách giải hợp lệ." } },
        StudentSubmission = new() { FinalAnswer = answer, ReasoningText = "Thay x=3 được 7", Confidence = 80 }, AllowedKnowledgeNodes = []
    };
    internal static object ResponseJson(string feedback, string language = "vi") => new
    {
        schemaVersion = AIAnalysisContract.SchemaVersion, language, methodDetected = "Thay giá trị", reasoningQuality = 100,
        errorType = "None", misconception = (string?)null, missingSteps = Array.Empty<string>(), rootCauseNodeIds = Array.Empty<string>(),
        confidence = 95, feedback, solutionType = "REFINED", aiSolution = "Thay số ta được $7$.", answerAssessment = "Correct", reasoningVerdict = "Valid"
    };
    private static ReasoningMicroBatcher Create(FakeExecutor executor, int size = 3, TimeProvider? clock = null) => new(executor,
        Options.Create(new AIGradingOptions { MicroBatchEnabled = true, BatchSize = size, BatchWindow = TimeSpan.FromMilliseconds(20) }), clock);

    private sealed class ManualBatchClock : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ManualTimer> _timers = [];
        private TimeSpan _elapsed;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Equal(Timeout.InfiniteTimeSpan, period); // Task.Delay uses one-shot timers.
            lock (_gate)
            {
                var timer = new ManualTimer(this, callback, state) { Due = _elapsed + dueTime };
                _timers.Add(timer); return timer;
            }
        }
        public void Advance(TimeSpan delta)
        {
            ManualTimer[] due;
            lock (_gate)
            {
                _elapsed += delta;
                due = _timers.Where(t => !t.Disposed && t.Due <= _elapsed).ToArray();
                foreach (var timer in due) timer.Disposed = true;
                _timers.RemoveAll(t => t.Disposed);
            }
            foreach (var timer in due) timer.Callback(timer.State);
        }
        private sealed class ManualTimer(ManualBatchClock owner, TimerCallback callback, object? state) : ITimer
        {
            public TimerCallback Callback { get; } = callback;
            public object? State { get; } = state;
            public TimeSpan Due { get; set; }
            public bool Disposed { get; set; }
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner._gate)
                {
                    if (Disposed) return false;
                    Due = dueTime == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue : owner._elapsed + dueTime;
                    return true;
                }
            }
            public void Dispose() { lock (owner._gate) Disposed = true; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    private static ReasoningBatchExecutor Executor(string raw) => new(
        Options.Create(new GeminiOptions { ApiKey = "fake", Model = "test-model" }), Options.Create(new AIGradingOptions()),
        new FakeClient(raw), null!, new(), new(), new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator()));
    private sealed class FakeClient(string raw) : IGeminiGenerateContentClient
    {
        public Task<GeminiGenerateContentResult> GenerateContentAsync(string model, string prompt, Google.GenAI.Types.GenerateContentConfig config, CancellationToken cancellationToken)
            => Task.FromResult(new GeminiGenerateContentResult(raw, 100, 100, 200));
    }
    private sealed class FakeExecutor : IReasoningBatchExecutor
    {
        public string ProfileVersion => "fake-test-v1";
        public string ProviderName => "Gemini";
        public string ModelName => "fake-model";
        public string? FailAnswer { get; init; }
        public ConcurrentBag<ReasoningBatchItem[]> Groups { get; } = [];
        public Task<IReadOnlyDictionary<string, ReasoningBatchResult>> ExecuteAsync(IReadOnlyList<ReasoningBatchItem> items, CancellationToken token)
        {
            Groups.Add(items.ToArray());
            return Task.FromResult<IReadOnlyDictionary<string, ReasoningBatchResult>>(items.ToDictionary(i => i.ItemId, i =>
                i.Request.StudentSubmission.FinalAnswer == FailAnswer
                    ? new ReasoningBatchResult(null, new InvalidOperationException("synthetic invalid item"))
                    : new ReasoningBatchResult(new StrictAIAnalysisResponseParser(new AnalyzeReasoningResponseValidator())
                        .ParseAndValidate(JsonSerializer.Serialize(ResponseJson(i.Request.StudentSubmission.FinalAnswer, i.Request.Language)), i.Request), null)));
        }
    }
}
