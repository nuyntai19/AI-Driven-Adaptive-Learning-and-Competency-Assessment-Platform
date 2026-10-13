using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorMySqlTests
{
    [MySqlIntegrationFact]
    public async Task ManualFallbackRecovery_MySqlCommitsCachedResultAndPreservesAppendOnlyHistory()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var center = Guid.NewGuid();
        await SeedAsync(database.ConnectionString, center, preliminaryCorrectness: null);
        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(center);

        // Create the same persisted fallback that a student can ask AI to regrade.
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            (await db.AIAnalysisJobs.SingleAsync()).RetryCount = 1;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted,
                (await CreateProcessor(db, tenant, new FailingAIService(), durable: true)
                    .ExecuteAsync(1, "mysql-worker", default)).Outcome);
        }

        ulong originalAnalysisId;
        ulong originalEvidenceId;
        ulong originalHistoryId;
        string originalEvidenceReasons;
        string originalHistoryBreakdown;
        string finalAnswer;
        string? reasoning;
        var retryTime = UtcNow.AddSeconds(30);
        var proposal = new AnalyzeReasoningResponse
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = "vi",
            MethodDetected = "Giải thích kết quả",
            ReasoningQuality = 90,
            ErrorType = ErrorType.None,
            MissingSteps = [],
            RootCauseNodeIds = [],
            Confidence = 95,
            Feedback = "Bài làm đúng và lập luận hợp lệ.",
            AiSolution = "Đối chiếu dữ kiện và áp dụng phương pháp phù hợp để tìm đáp án.",
            AnswerAssessment = "Correct",
            ReasoningVerdict = "Valid",
            SuggestedScore = 1,
            ReasoningIssues = []
        };

        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            var attempt = await db.Attempts.SingleAsync();
            var existing = await db.ReasoningAnalyses.SingleAsync();
            var evidence = await db.EvidenceAssessments.SingleAsync();
            var history = await db.TwinUpdateHistories.SingleAsync();
            originalAnalysisId = existing.AnalysisId;
            originalEvidenceId = evidence.EvidenceAssessmentId;
            originalHistoryId = history.HistoryId;
            originalEvidenceReasons = evidence.ReasonCodes.RootElement.GetRawText();
            originalHistoryBreakdown = history.CalculationBreakdown.RootElement.GetRawText();
            finalAnswer = attempt.FinalAnswer;
            reasoning = attempt.ReasoningText;

            attempt.ManualRetryCount = 1;
            attempt.LastManualRetryAt = retryTime;
            attempt.Status = AttemptStatus.PendingAnalysis;
            var job = await db.AIAnalysisJobs.SingleAsync();
            job.Status = AIJobStatus.Processing;
            job.RetryCount = 0;
            job.AvailableAt = retryTime;
            job.StartedAt = retryTime;
            job.CompletedAt = null;
            job.LeaseOwner = "mysql-worker";
            job.LeaseUntil = retryTime.AddMinutes(5);
            job.LastErrorCode = null;
            job.LastErrorMessage = null;
            await db.SaveChangesAsync();

            // Simulate a provider result checkpointed before a failed relational commit.
            var question = await db.Questions.SingleAsync();
            var request = new AIAnalysisRequestFactory().Create(attempt, question,
                await db.KnowledgeNodes.ToListAsync());
            var fingerprint = AIAnalysisCheckpointStore.Fingerprint(request,
                AIAnalysisContract.SchemaVersion, question.RowVersion);
            Assert.True(await new AIAnalysisCheckpointStore(db, new FixedTimeProvider(retryTime))
                .SaveAsync(job, "mysql-worker", fingerprint, proposal, retryTime, default));
        }

        var provider = new NeverCalledRecoveryAIService();
        await using (var db = CreateContext(database.ConnectionString, tenant))
        {
            Assert.Equal(AIAnalysisJobProcessingOutcome.Completed,
                (await CreateProcessor(db, tenant, provider,
                    timeProvider: new FixedTimeProvider(retryTime), durable: true)
                    .ExecuteAsync(1, "mysql-worker", default)).Outcome);
        }

        await using var verify = CreateContext(database.ConnectionString, tenant);
        Assert.Equal(0, provider.CallCount);
        var savedAttempt = await verify.Attempts.SingleAsync();
        Assert.Equal(finalAnswer, savedAttempt.FinalAnswer);
        Assert.Equal(reasoning, savedAttempt.ReasoningText);
        Assert.Equal(1, savedAttempt.ManualRetryCount);
        Assert.NotEqual(AttemptStatus.PendingAnalysis, savedAttempt.Status);
        Assert.Equal(AIJobStatus.Completed, (await verify.AIAnalysisJobs.SingleAsync()).Status);
        var saved = await verify.ReasoningAnalyses.SingleAsync();
        Assert.Equal(originalAnalysisId, saved.AnalysisId);
        Assert.False(saved.IsFallback);
        Assert.Equal(proposal.Feedback, saved.Feedback);
        Assert.Equal(1m, saved.SuggestedScore);
        Assert.Null(saved.OverrideAwardedScore); // AI proposes; the teacher still decides.
        var evidences = await verify.EvidenceAssessments.OrderBy(e => e.EvidenceAssessmentId).ToListAsync();
        Assert.Equal(2, evidences.Count);
        Assert.Equal(originalEvidenceId, evidences[0].EvidenceAssessmentId);
        Assert.Equal(originalEvidenceReasons, evidences[0].ReasonCodes.RootElement.GetRawText());
        Assert.Equal(originalEvidenceId, evidences[1].SupersedesAssessmentId);
        var histories = await verify.TwinUpdateHistories.OrderBy(h => h.HistoryId).ToListAsync();
        Assert.Equal(2, histories.Count);
        Assert.Equal(originalHistoryId, histories[0].HistoryId);
        Assert.Equal(originalHistoryBreakdown, histories[0].CalculationBreakdown.RootElement.GetRawText());
        Assert.Equal("fallback-replay-v1", histories[1].CalculationVersion);
        Assert.True(histories[1].CalculationVersion.Length <= 20);
        Assert.True(histories[1].CalculationBreakdown.RootElement.TryGetProperty("PreviousFallbackAnalysis", out _));
        Assert.Equal(1u, (await verify.BehaviorTwins.SingleAsync()).AttemptCount);
        Assert.Empty(await verify.AIAnalysisCheckpoints.ToListAsync());

        // A reclaimed/duplicate execution cannot append another assessment or history.
        Assert.Equal(AIAnalysisJobProcessingOutcome.AlreadyTerminal,
            (await CreateProcessor(verify, tenant, provider,
                timeProvider: new FixedTimeProvider(retryTime), durable: true)
                .ExecuteAsync(1, "mysql-worker", default)).Outcome);
        Assert.Equal(2, await verify.EvidenceAssessments.CountAsync());
        Assert.Equal(2, await verify.TwinUpdateHistories.CountAsync());
        Assert.Equal(0, provider.CallCount);
    }

    private sealed class NeverCalledRecoveryAIService : IAIService
    {
        public int CallCount { get; private set; }

        public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
            AnalyzeReasoningRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("A valid checkpoint must be reused.");
        }
    }
}
