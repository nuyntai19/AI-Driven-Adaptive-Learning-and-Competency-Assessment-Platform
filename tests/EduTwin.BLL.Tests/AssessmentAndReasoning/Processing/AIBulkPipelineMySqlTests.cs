using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.API.AssessmentAndReasoning.Background;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Google.GenAI.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorMySqlTests
{
    [MySqlIntegrationFact]
    public Task BulkPipeline50_RealQueueMicrobatchCheckpointEvidenceTwinAndProgress() => RunBulkPipelineAsync(50, false);
    [MySqlIntegrationFact]
    public Task BulkPipeline100_QuotaAndPartialResponseRecoverWithoutDuplicateEvidence() => RunBulkPipelineAsync(100, true);

    private static async Task RunBulkPipelineAsync(int count, bool injectFaults)
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var center = Guid.NewGuid(); await SeedAsync(database.ConnectionString, center);
        var storage = new BulkStorage(); var assignment = Guid.NewGuid(); Guid student; Guid subject;
        var seedTenant = new TenantContext(); using var seedScope = seedTenant.BeginScope(center);
        await using (var db = CreateContext(database.ConnectionString, seedTenant))
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=0;");
            try
            {
                var first = await db.Attempts.SingleAsync(); student = first.StudentId;
                var question = await db.Questions.SingleAsync(); subject = question.SubjectId;
                var job = await db.AIAnalysisJobs.SingleAsync();
                db.Assignments.Add(new Assignment { AssignmentId = assignment, CenterId = center, ClassId = Guid.NewGuid(),
                    CreatedByTeacherId = question.CreatedByTeacherId, Title = "Synthetic bulk pipeline", Status = AssignmentStatus.Published,
                    CreatedAt = UtcNow.AddDays(-1), UpdatedAt = UtcNow.AddDays(-1) });
                db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { CenterId = center, AssignmentId = assignment,
                    StudentId = student, TotalQuestionCount = (uint)count, Status = ProgressStatus.InProgress,
                    StartedAt = UtcNow.AddMinutes(-2), CreatedAt = UtcNow.AddMinutes(-2), UpdatedAt = UtcNow.AddMinutes(-2) });
                for (var i = 1; i <= count; i++)
                {
                    var q = i == 1 ? question : (Question)db.Entry(question).CurrentValues.ToObject();
                    q.QuestionId = (ulong)i; q.QuestionText = $"Cho $y=2x+1$, tính $y$ khi $x=3$. [bulk:{i}]";
                    q.CorrectAnswer = "7"; q.Solution = $"$2*3+1=7$. [reference:{i}]";
                    q.QuestionType = (QuestionType)(i % 3); q.MaxScore = i % 2 == 0 ? 20 : 10;
                    q.ExpectedReasoning = "Thay giá trị x vào hàm số."; q.RowVersion = 1;
                    if (i > 1)
                    {
                        db.Questions.Add(q);
                        db.QuestionKnowledgeNodes.Add(new() { CenterId = center, QuestionId = (ulong)i, NodeId = 1,
                            MappingRole = MappingRole.Primary, CreatedAt = UtcNow.AddDays(-1) });
                    }
                    var a = i == 1 ? first : (Attempt)db.Entry(first).CurrentValues.ToObject();
                    a.AttemptId = (ulong)i; a.QuestionId = (ulong)i; a.AssignmentId = assignment;
                    a.FinalAnswer = "7"; a.ReasoningText = $"$y=2*3+1=7$. [submission:{i}]";
                    if (q.QuestionType == QuestionType.MultipleChoice)
                    {
                        db.QuestionOptions.Add(new() { CenterId = center, QuestionId = q.QuestionId, OptionId = (ulong)(10000+i),
                            OptionLabel = "A", OptionText = "7", IsCorrect = true, OrderIndex = 1, CreatedAt = UtcNow, UpdatedAt = UtcNow });
                        a.FinalAnswer = (10000+i).ToString();
                    }
                    a.AwardedScore = q.MaxScore; a.ClientSubmissionId = Guid.NewGuid(); a.RowVersion = 1;
                    a.CreatedAt = UtcNow.AddMinutes(-2).AddMilliseconds(i); a.UpdatedAt = a.CreatedAt;
                    if (i > 1) db.Attempts.Add(a);
                    var j = i == 1 ? job : (AIAnalysisJob)db.Entry(job).CurrentValues.ToObject();
                    j.AnalysisJobId = (ulong)i; j.AttemptId = (ulong)i; j.Status = AIJobStatus.Pending;
                    j.LeaseOwner = null; j.LeaseUntil = null; j.StartedAt = null; j.RowVersion = 1;
                    j.CorrelationId = $"synthetic-bulk-{i}"; j.CreatedAt = a.CreatedAt; j.AvailableAt = a.CreatedAt;
                    if (i > 1) db.AIAnalysisJobs.Add(j);
                    db.AssignmentQuestions.Add(new() { CenterId = center, AssignmentId = assignment, QuestionId = (ulong)i,
                        OrderIndex = (uint)i, Points = 1, CreatedAt = UtcNow.AddDays(-1) });
                    if (i % 5 == 0)
                    {
                        var key = $"synthetic/{center:N}/{i}.png";
                        storage.Bytes[key] = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lR8AAAAASUVORK5CYII=").Concat(BitConverter.GetBytes(i)).ToArray();
                        db.AttemptAttachments.Add(new() { CenterId = center, AttemptId = (ulong)i, FileName = "synthetic.png",
                            StorageKey = key, ContentType = "image/png", FileSizeBytes = storage.Bytes[key].Length,
                            UploadNonce = Guid.NewGuid().ToString("N"), CreatedAt = a.CreatedAt });
                    }
                }
                await db.SaveChangesAsync();
            }
            finally { await db.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS=1;"); }
        }

        var clock = new BulkClock(UtcNow); var observations = new BulkObservations();
        var services = new ServiceCollection(); services.AddLogging(); services.AddIdentityAndTenancy();
        services.AddSingleton<TimeProvider>(clock); services.AddSingleton(observations);
        services.AddDbContext<EduTwinDbContext>(o => o.UseMySQL(database.ConnectionString));
        services.AddAssessmentAndReasoning(); services.AddSingleton<IAttemptAttachmentStorage>(storage);
        services.AddScoped<IRecommendationEngine>(s => CreateRecommendationEngine(s.GetRequiredService<EduTwinDbContext>()));
        services.AddScoped<IOverallAssignmentCommentWorkflow, BulkComments>();
        services.AddSingleton(Options.Create(new GeminiOptions { ApiKey = "synthetic-not-a-key", Model = "synthetic-bulk" }));
        services.AddSingleton(Options.Create(new AIGradingOptions { MicroBatchEnabled = true, BatchSize = 3 }));
        services.AddSingleton<GeminiQuotaCoordinator>();
        services.AddSingleton<IGeminiGenerateContentClient>(s => new BulkTransport(s.GetRequiredService<GeminiQuotaCoordinator>(), observations, injectFaults));
        services.AddSingleton<GeminiPromptBuilder>(); services.AddSingleton<GeminiResponseJsonSchema>();
        services.AddSingleton<IAnalyzeReasoningResponseValidator, AnalyzeReasoningResponseValidator>();
        services.AddSingleton<IAIAnalysisResponseParser, StrictAIAnalysisResponseParser>();
        services.AddSingleton<IReasoningBatchExecutor>(s => new ReasoningBatchExecutor(s.GetRequiredService<IOptions<GeminiOptions>>(),
            s.GetRequiredService<IOptions<AIGradingOptions>>(), s.GetRequiredService<IGeminiGenerateContentClient>(), null!, new(), new(),
            s.GetRequiredService<IAIAnalysisResponseParser>()));
        services.AddSingleton<ReasoningMicroBatcher>(); services.AddSingleton<IAIService, GeminiAIService>();
        services.AddScoped<AIAnalysisJobProcessor>();
        services.AddScoped<IAIAnalysisJobProcessor, BulkTracingProcessor>();
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var grading = provider.GetRequiredService<IOptions<AIGradingOptions>>().Value;
        var worker = new AIAnalysisJobBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), clock,
            new() { BatchSize = 50, PerCenterBatchSize = grading.RecommendedPerCenterJobs, MaxConcurrentJobs = grading.RecommendedJobConcurrency },
            new("synthetic-bulk-worker"), NullLogger<AIAnalysisJobBackgroundService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        observations.Elapsed.Start(); var cycles = 0; var retryScheduled = 0;
        while (observations.CompletionMs.Count < count && cycles++ < count * 3)
        {
            var result = await worker.RunBatchOnceAsync(timeout.Token);
            Assert.Equal(0, result.ExceptionCount); Assert.Equal(0, result.FallbackCompletedCount);
            retryScheduled += result.RetryScheduledCount; clock.Advance(TimeSpan.FromSeconds(5));
        }
        observations.Elapsed.Stop(); Assert.Equal(count, observations.CompletionMs.Count);
        var tenantContext = new TenantContext(); using var scope = tenantContext.BeginScope(center);
        await using var verify = CreateContext(database.ConnectionString, tenantContext);
        var analyses = await verify.ReasoningAnalyses.OrderBy(x => x.AttemptId).ToListAsync();
        Assert.Equal(count, analyses.Count); Assert.All(analyses, a => { Assert.False(a.IsFallback); Assert.Equal("synthetic-bulk", a.ModelName);
            Assert.Contains("microbatch-v1", a.AnalysisProfileVersion); Assert.Contains($"reference:{a.AttemptId}", a.Feedback);
            Assert.Contains($"submission:{a.AttemptId}", a.Feedback); });
        Assert.Equal(count, await verify.EvidenceAssessments.CountAsync());
        Assert.Equal(count, await verify.TwinUpdateHistories.CountAsync()); Assert.Empty(await verify.AIAnalysisCheckpoints.ToListAsync());
        Assert.Equal((uint)count, (await verify.BehaviorTwins.SingleAsync()).AttemptCount);
        Assert.Equal((uint)count, (await verify.KnowledgeTwins.SingleAsync()).EvidenceCount);
        Assert.Equal((ulong)count, (await verify.KnowledgeTwins.SingleAsync()).LastAttemptId);
        var history = await verify.TwinUpdateHistories.OrderBy(x => x.HistoryId).ToListAsync();
        Assert.Equal(Enumerable.Range(1, count).Select(i => (ulong?)i), history.Select(h => h.AttemptId));
        for (var i = 1; i < history.Count; i++) Assert.Equal(history[i-1].NewMastery, history[i].PreviousMastery);
        var progress = await verify.StudentAssignmentProgresses.SingleAsync();
        Assert.Equal((uint)count, progress.CompletedQuestionCount); Assert.Equal(ProgressStatus.Completed, progress.Status);
        Assert.Equal(TeacherFinalReviewStatus.Pending, progress.TeacherFinalReviewStatus);
        Assert.All(await verify.Attempts.ToListAsync(), a => Assert.Equal(a.QuestionId % 2 == 0 ? 20m : 10m, a.AwardedScore));
        Assert.Equal(count / 5, observations.ImageHashes.Count);
        foreach (var pair in storage.Bytes)
        {
            var i = int.Parse(pair.Key.Split('/').Last().Replace(".png", ""));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(pair.Value)), observations.ImageHashes[i]);
        }
        Assert.True(observations.ProviderCalls < count); Assert.All(observations.GroupSizes, s => Assert.InRange(s, 1, 3));
        Assert.Equal(injectFaults ? 2 : 1, observations.Delivered[10]);
        Assert.All(observations.Delivered.Where(p => p.Key != 10), p => Assert.Equal(1, p.Value));
        var post = new AIStudentPostProcessor(verify, CreateRecommendationEngine(verify), new BulkComments(verify, clock, observations), clock);
        Assert.True(await post.RunOneAsync(student, subject, assignment, "bulk-post", timeout.Token));
        verify.ChangeTracker.Clear(); var postJob = await verify.AIStudentPostProcessingJobs.SingleAsync();
        Assert.Equal((ulong)count, postJob.Revision); Assert.Equal(postJob.Revision, postJob.ProcessedRevision);
        Assert.Equal(1, observations.CommentCalls); Assert.NotNull((await verify.StudentAssignmentProgresses.SingleAsync()).OverallAiComment);
        Assert.Equal((ulong)count, (await verify.RecommendationGenerationStates.SingleAsync()).LastSourceAttemptId);
        var report = new { kind = "real-mysql-processing-pipeline-synthetic-provider", count, injectFaults, cycles,
            providerCalls = observations.ProviderCalls, singletonBatches = observations.GroupSizes.Count(n => n == 1),
            averageBatchSize = observations.GroupSizes.Average(), images = observations.ImageHashes.Count, retryScheduled,
            elapsedMs = observations.Elapsed.ElapsedMilliseconds, completionP50Ms = Percentile(observations.CompletionMs, .5),
            completionP95Ms = Percentile(observations.CompletionMs, .95), evidence = count, twinHistory = count,
            duplicateEvidence = 0, duplicateHistory = 0, fallback = 0, productionDataTouched = false };
        Console.WriteLine(JsonSerializer.Serialize(report));
    }

    private static double Percentile(IEnumerable<double> values, double p) { var a = values.Order().ToArray(); return a[(int)Math.Ceiling(a.Length*p)-1]; }
    private sealed class BulkClock(DateTime now) : TimeProvider { private long _ticks = now.Ticks;
        public override DateTimeOffset GetUtcNow() => new(new DateTime(Interlocked.Read(ref _ticks), DateTimeKind.Utc));
        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks); }
    private sealed class BulkObservations
    {
        public readonly Stopwatch Elapsed = new(); public readonly ConcurrentBag<double> CompletionMs = [];
        public readonly ConcurrentBag<int> GroupSizes = []; public readonly ConcurrentDictionary<int, int> Delivered = [];
        public readonly ConcurrentDictionary<int, string> ImageHashes = []; public int ProviderCalls; public int CommentCalls;
    }
    private sealed class BulkTracingProcessor(AIAnalysisJobProcessor inner, BulkObservations observations) : IAIAnalysisJobProcessor
    {
        public async Task<AIAnalysisJobProcessingResult> ExecuteAsync(ulong id, string owner, CancellationToken token)
        { var result = await inner.ExecuteAsync(id, owner, token); if (result.Outcome == AIAnalysisJobProcessingOutcome.Completed)
                observations.CompletionMs.Add(observations.Elapsed.Elapsed.TotalMilliseconds); return result; }
    }
    private sealed class BulkTransport(GeminiQuotaCoordinator quota, BulkObservations observations, bool faults) : IGeminiGenerateContentClient
    {
        private int _badResponse;
        public Task<GeminiGenerateContentResult> GenerateContentAsync(string model, string prompt, GenerateContentConfig config, CancellationToken token)
            => GenerateContentWithImagesAsync(model, prompt, [], config, token);
        public async Task<GeminiGenerateContentResult> GenerateContentWithImagesAsync(string model, string prompt,
            IReadOnlyList<GeminiInlineImagePart> images, GenerateContentConfig config, CancellationToken token)
        {
            var pool = new GeminiQuotaPoolOptions { ProjectId = "synthetic-bulk-test", KeyIndexes = [0], MaxConcurrentRequests = 2 };
            GeminiQuotaLease lease;
            try { lease = await quota.AcquireAsync(pool, model, 100, TimeSpan.FromSeconds(30), token, globalMaxConcurrentRequests: 2); }
            catch (Exception ex) when (ex is not (OperationCanceledException or AIAnalysisDeferredException or GeminiAdapterException))
            { throw new AIAnalysisInfrastructureException(); }
            var call = Interlocked.Increment(ref observations.ProviderCalls); var fail = faults && call == 1;
            try
            {
                if (fail) throw new AIAnalysisDeferredException(TimeSpan.FromSeconds(60));
                await Task.Delay(10, token);
                var batch = prompt.Contains("BATCH_INPUT_JSON_BEGIN", StringComparison.Ordinal);
                var begin = batch ? "BATCH_INPUT_JSON_BEGIN\n" : "INPUT_JSON_BEGIN\n";
                var end = batch ? "\nBATCH_INPUT_JSON_END" : "\nINPUT_JSON_END";
                var raw = prompt[(prompt.IndexOf(begin, StringComparison.Ordinal)+begin.Length)..prompt.IndexOf(end, StringComparison.Ordinal)];
                using var doc = JsonDocument.Parse(raw);
                var items = batch ? doc.RootElement.EnumerateArray().ToArray() : [doc.RootElement];
                observations.GroupSizes.Add(items.Length); var results = new List<object>(); object? single = null;
                foreach (var item in items)
                {
                    var input = batch ? item.GetProperty("input") : item;
                    var question = input.GetProperty("question"); var submission = input.GetProperty("studentSubmission");
                    var marker = question.GetProperty("questionText").GetString()!; var i = int.Parse(marker.Split("bulk:")[1].TrimEnd(']'));
                    Assert.Equal($"$2*3+1=7$. [reference:{i}]", question.GetProperty("solution").GetString());
                    Assert.Equal($"$y=2*3+1=7$. [submission:{i}]", submission.GetProperty("reasoningText").GetString());
                    var positions = batch ? item.GetProperty("imageIndexes").EnumerateArray().Select(p => p.GetInt32()-1).ToArray()
                        : Enumerable.Range(0, images.Count).ToArray();
                    Assert.Equal(i % 5 == 0 ? 1 : 0, positions.Length);
                    foreach (var position in positions) observations.ImageHashes[i] = Convert.ToHexString(SHA256.HashData(images[position].Data));
                    observations.Delivered.AddOrUpdate(i, 1, (_, old) => old+1);
                    var bad = faults && i == 10 && Interlocked.Exchange(ref _badResponse, 1) == 0;
                    single = new { schemaVersion = AIAnalysisContract.SchemaVersion, language = "vi", methodDetected = "Thay giá trị",
                        reasoningQuality = bad ? 101 : 90, errorType = "None", misconception = (string?)null, missingSteps = Array.Empty<string>(),
                        rootCauseNodeIds = Array.Empty<string>(), confidence = 95, feedback = $"Lập luận đúng. reference:{i};submission:{i}; phản hồi giả lập.",
                        solutionType = "REFINED", aiSolution = "Thay giá trị: $y=2(3)+1=7$.", answerAssessment = "Correct", reasoningVerdict = "Valid",
                        suggestedScore = question.GetProperty("maxScore").GetDecimal(), usesAlternativeMethod = false, suggestedRubricScores = Array.Empty<object>(),
                        reasoningIssues = Array.Empty<object>() };
                    if (batch) results.Add(new { itemId = item.GetProperty("itemId").GetString(), analysis = single });
                }
                return new(JsonSerializer.Serialize(batch ? new { results = results.AsEnumerable().Reverse().ToArray() } : single), 100, 100, 200);
            }
            finally
            {
                // Match the production client's release semantics; a release outage
                // must not turn a valid inference into a failed grading attempt.
                try { await quota.CompleteAsync(lease, 100, fail, fail, CancellationToken.None); }
                catch (Exception) { }
            }
        }
    }
    private sealed class BulkStorage : IAttemptAttachmentStorage
    {
        public readonly Dictionary<string, byte[]> Bytes = [];
        public Task<Stream> OpenPermanentReadAsync(string key, CancellationToken token) => Task.FromResult<Stream>(new MemoryStream(Bytes[key], false));
        public Task<StoredTemporaryAttachment> StoreTemporaryPngAsync(Guid centerId, string nonce, Stream content, CancellationToken token) => throw new NotSupportedException();
        public Task<PromotedAttemptAttachment> PromoteToPermanentAsync(AttachmentUploadTokenPayload p, CancellationToken token) => throw new NotSupportedException();
        public Task DeletePermanentAsync(string key, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class BulkComments(EduTwinDbContext db, TimeProvider clock, BulkObservations observations) : IOverallAssignmentCommentWorkflow
    {
        public Task InvalidateCommentAsync(Guid c, Guid a, Guid s, CancellationToken t) => Task.CompletedTask;
        public async Task<string?> GenerateAndCacheOverallCommentAsync(Guid c, Guid a, Guid s, CancellationToken t)
        {
            var progress = await db.StudentAssignmentProgresses.SingleAsync(p => p.CenterId == c && p.AssignmentId == a && p.StudentId == s, t);
            progress.OverallAiComment = "Synthetic aggregate; no provider call."; progress.OverallAiCommentGeneratedAt = clock.GetUtcNow().UtcDateTime;
            progress.OverallAiCommentVersion++; progress.IsOverallAiCommentStale = false; await db.SaveChangesAsync(t);
            Interlocked.Increment(ref observations.CommentCalls); return progress.OverallAiComment;
        }
    }
}
