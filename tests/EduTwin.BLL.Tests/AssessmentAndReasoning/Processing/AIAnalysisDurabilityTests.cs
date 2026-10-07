using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.DigitalTwin.Orchestration;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorTests
{
    [Fact]
    public async Task Durable_MultipleChoiceStillReceivesIndividualReasoningAnalysis()
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(store, name, tenant);
        var question = await db.Questions.SingleAsync(); question.QuestionType = QuestionType.MultipleChoice;
        var attempt = await db.Attempts.SingleAsync(); attempt.FinalAnswer = "1001";
        db.QuestionOptions.Add(new QuestionOption { CenterId = center, QuestionId = question.QuestionId, OptionId = 1001,
            OptionLabel = "A", OptionText = "42", IsCorrect = true, OrderIndex = 1, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ai = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));
        Assert.Equal(AIAnalysisJobProcessingOutcome.Completed,
            (await CreateSut(db, tenant, UtcNow, ai, durable: true).ExecuteAsync(1, "worker-current", CancellationToken.None)).Outcome);
        Assert.Equal(1, ai.CallCount); Assert.Equal(QuestionType.MultipleChoice, ai.Request!.Question.QuestionType);
        Assert.Equal("A. 42", ai.Request.StudentSubmission.FinalAnswer);
        Assert.Equal("A. 42", ai.Request.Question.CorrectAnswer);
        Assert.Equal(1, await db.EvidenceAssessments.CountAsync());
    }

    [Fact]
    public async Task Durable_LegacyQuestionWithoutMapping_UsesOnlyItsActiveSameTenantPrimaryTopic()
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(store, name, tenant);
        db.QuestionKnowledgeNodes.RemoveRange(await db.QuestionKnowledgeNodes.ToListAsync());
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ai = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));
        Assert.Equal(AIAnalysisJobProcessingOutcome.Completed,
            (await CreateSut(db, tenant, UtcNow, ai, durable: true).ExecuteAsync(1, "worker-current", default)).Outcome);
        Assert.Equal(new[] { "20" }, ai.Request!.AllowedKnowledgeNodes.Select(n => n.NodeId));
        Assert.False((await db.ReasoningAnalyses.SingleAsync()).IsFallback);
    }

    [Fact]
    public async Task CheckpointFingerprintChangesWithImageAnswerReferenceProfileAndQuestionVersion()
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(store, name, tenant);
        var ai = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));
        await CreateSut(db, tenant, UtcNow, ai, durable: true).ExecuteAsync(1, "worker-current", CancellationToken.None);
        var request = ai.Request!; var original = AIAnalysisCheckpointStore.Fingerprint(request, "profile", 1);
        Assert.NotEqual(original, AIAnalysisCheckpointStore.Fingerprint(request, "different-model-or-prompt", 1));
        Assert.NotEqual(original, AIAnalysisCheckpointStore.Fingerprint(request, "profile", 2));
        Assert.NotEqual(original, AIAnalysisCheckpointStore.Fingerprint(request with { Question = request.Question with { Solution = "different reference" } }, "profile", 1));
        Assert.NotEqual(original, AIAnalysisCheckpointStore.Fingerprint(request with { StudentSubmission = request.StudentSubmission with { FinalAnswer = "changed" } }, "profile", 1));
        var withImage = request with { StudentSubmission = request.StudentSubmission with { ImageParts = [new AnalyzeReasoningImagePart([1, 2, 3], "image/png")] } };
        Assert.NotEqual(original, AIAnalysisCheckpointStore.Fingerprint(withImage, "profile", 1));
        Assert.NotEqual(AIAnalysisCheckpointStore.Fingerprint(withImage, "profile", 1),
            AIAnalysisCheckpointStore.Fingerprint(withImage with { StudentSubmission = withImage.StudentSubmission with { ImageParts = [new AnalyzeReasoningImagePart([1, 2, 4], "image/png")] } }, "profile", 1));
    }

    [Fact]
    public async Task Durable_ResultSurvivesFailedCompletion_WithoutRepeatingProviderCall()
    {
        var store = new InMemoryDatabaseRoot();
        var name = Guid.NewGuid().ToString();
        var center = Guid.NewGuid();
        await SeedAsync(store, name, center);
        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(center);
        var ai = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));
        var broken = new Mock<ITwinCompletionOrchestrator>();
        broken.Setup(x => x.CompleteAsync(It.IsAny<Attempt>(), It.IsAny<Question>(), It.IsAny<ReasoningAnalysis>(),
            It.IsAny<TwinEventSource>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyCollection<ulong>?>()))
            .ThrowsAsync(new InvalidOperationException("synthetic persistence outage"));
        await using (var db = CreateContext(store, name, tenant))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateSut(db, tenant, UtcNow, ai, durable: true, completion: broken.Object)
                    .ExecuteAsync(1, "worker-current", CancellationToken.None));
            Assert.Single(await db.AIAnalysisCheckpoints.ToListAsync());
            Assert.Empty(await db.ReasoningAnalyses.ToListAsync());
        }
        var neverCall = new RecordingAIService((_, _) => throw new InvalidOperationException("must reuse checkpoint"));
        await using (var db = CreateContext(store, name, tenant))
        {
            var result = await CreateSut(db, tenant, UtcNow, neverCall, durable: true).ExecuteAsync(1, "worker-current", CancellationToken.None);
            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed, result.Outcome);
            Assert.Single(await db.ReasoningAnalyses.ToListAsync());
            Assert.Empty(await db.AIAnalysisCheckpoints.ToListAsync());
            var queued = Assert.Single(await db.AIStudentPostProcessingJobs.ToListAsync());
            Assert.Equal(1ul, queued.Revision);
            Assert.Equal(0ul, queued.ProcessedRevision);
        }
    }

    [Fact]
    public async Task Durable_LaterInferenceWaitsForEarlierEvidence_ThenUsesCheckpoint()
    {
        var store = new InMemoryDatabaseRoot();
        var name = Guid.NewGuid().ToString();
        var center = Guid.NewGuid();
        await SeedAsync(store, name, center);
        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(center);
        await using (var db = CreateContext(store, name, tenant))
        {
            var first = await db.Attempts.SingleAsync();
            var second = (Attempt)db.Entry(first).CurrentValues.ToObject();
            second.AttemptId = 2; second.ClientSubmissionId = Guid.NewGuid(); second.CreatedAt = first.CreatedAt.AddSeconds(1);
            db.Attempts.Add(second);
            var firstJob = await db.AIAnalysisJobs.SingleAsync();
            var secondJob = (AIAnalysisJob)db.Entry(firstJob).CurrentValues.ToObject();
            secondJob.AnalysisJobId = 2; secondJob.AttemptId = 2;
            db.AIAnalysisJobs.Add(secondJob);
            await db.SaveChangesAsync();
        }
        var ai = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));
        await using (var db = CreateContext(store, name, tenant))
        {
            var result = await CreateSut(db, tenant, UtcNow, ai, durable: true).ExecuteAsync(2, "worker-current", CancellationToken.None);
            Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled, result.Outcome);
            Assert.Empty(await db.ReasoningAnalyses.ToListAsync());
            Assert.Single(await db.AIAnalysisCheckpoints.ToListAsync());
            Assert.Equal("AI_WAITING_FOR_EARLIER_EVIDENCE", (await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2)).LastErrorCode);
        }
        await using (var db = CreateContext(store, name, tenant))
            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed,
                (await CreateSut(db, tenant, UtcNow, ai, durable: true).ExecuteAsync(1, "worker-current", CancellationToken.None)).Outcome);
        await using (var db = CreateContext(store, name, tenant))
        {
            var job = await db.AIAnalysisJobs.SingleAsync(x => x.AnalysisJobId == 2);
            job.Status = AIJobStatus.Processing; job.LeaseOwner = "worker-current"; job.LeaseUntil = UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var neverCall = new RecordingAIService((_, _) => throw new InvalidOperationException("checkpoint must be used"));
            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed,
                (await CreateSut(db, tenant, UtcNow.AddSeconds(2), neverCall, durable: true).ExecuteAsync(2, "worker-current", CancellationToken.None)).Outcome);
            Assert.Equal(2, await db.ReasoningAnalyses.CountAsync());
            Assert.Equal(2ul, (await db.AIStudentPostProcessingJobs.SingleAsync()).Revision);
        }
    }

    [Fact]
    public async Task Durable_CapacityWaitDoesNotConsumeFailureRetryOrGenerateFallback()
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center, retryCount: 0);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(store, name, tenant);
        var ai = new RecordingAIService((_, _) => throw new AIAnalysisDeferredException(TimeSpan.FromSeconds(60)));
        var result = await CreateSut(db, tenant, UtcNow, ai, durable: true).ExecuteAsync(1, "worker-current", CancellationToken.None);
        Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled, result.Outcome);
        var job = await db.AIAnalysisJobs.SingleAsync();
        Assert.Equal(0, job.RetryCount); Assert.Equal(UtcNow.AddSeconds(60), job.AvailableAt);
        Assert.Null(job.LeaseOwner); Assert.Empty(await db.ReasoningAnalyses.ToListAsync());
    }

    [Fact]
    public async Task Durable_CheckpointDatabaseFailureIsNotAnAIGradingFailure()
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center, retryCount: 0);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(store, name, tenant);
        var broken = new Mock<IAIAnalysisCheckpointStore>();
        broken.Setup(x => x.ReadAsync(center, 1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic database unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(db, tenant, UtcNow, durable: true, checkpoints: broken.Object).ExecuteAsync(1, "worker-current", CancellationToken.None));
        Assert.Equal(0, (await db.AIAnalysisJobs.SingleAsync()).RetryCount);
        Assert.Empty(await db.ReasoningAnalyses.ToListAsync());
    }

    [Fact]
    public async Task Durable_ExpiredOwnerCannotOverwriteNewCheckpoint()
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString(); var center = Guid.NewGuid();
        await SeedAsync(store, name, center);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(center);
        await using var db = CreateContext(store, name, tenant);
        var job = await db.AIAnalysisJobs.AsNoTracking().SingleAsync();
        var checkpoints = new AIAnalysisCheckpointStore(db, new FixedTimeProvider(UtcNow.AddMinutes(6)));
        Assert.False(await checkpoints.SaveAsync(job, "worker-current", "old", ValidResponse("vi"), UtcNow, CancellationToken.None));
        Assert.Empty(await db.AIAnalysisCheckpoints.ToListAsync());
    }
}
