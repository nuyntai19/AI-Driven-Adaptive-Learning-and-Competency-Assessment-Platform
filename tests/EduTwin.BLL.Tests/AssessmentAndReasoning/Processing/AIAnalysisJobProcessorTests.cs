using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed class AIAnalysisJobProcessorTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public async Task ExecuteAsync_ValidAIResponse_PersistsCompletedCheckpointWithMinimalOrderedContext(
        string language)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            retryCount: 0,
            language: language);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        using var cancellation = new CancellationTokenSource();
        var aiService = new RecordingAIService((request, token) =>
        {
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Equal(cancellation.Token, token);
            return Task.FromResult(ValidResponse(language));
        });

        var result = await CreateSut(context, tenant, UtcNow, aiService).ExecuteAsync(
            1,
            "worker-current",
            cancellation.Token);

        Assert.Equal(AIAnalysisJobProcessingOutcome.Completed, result.Outcome);
        Assert.Equal(1, aiService.CallCount);
        var request = Assert.IsType<AnalyzeReasoningRequest>(aiService.Request);
        Assert.Equal(language, request.Language);
        Assert.Equal(["10", "20"], request.AllowedKnowledgeNodes.Select(node => node.NodeId));
        Assert.Equal(["Secondary mapped", "Primary mapped"], request.AllowedKnowledgeNodes.Select(node => node.NodeName));
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(AttemptStatus.Completed, persisted.Attempt.Status);
        Assert.Equal(AIJobStatus.Completed, persisted.Job.Status);
        Assert.Equal((byte)0, persisted.Job.RetryCount);
        Assert.Null(persisted.Job.LeaseOwner);
        Assert.Null(persisted.Job.LeaseUntil);
        Assert.Null(persisted.Job.LastErrorCode);
        Assert.Null(persisted.Job.LastErrorMessage);
        var analysis = Assert.Single(persisted.Analyses);
        Assert.False(analysis.IsFallback);
        Assert.False(analysis.NeedsTeacherReview);
        Assert.Equal(AnalysisProvider.Gemini, analysis.Provider);
        Assert.Equal(84m, analysis.ReasoningQuality);
        Assert.Equal(["20"], analysis.RootCauseNodeIds.RootElement.EnumerateArray().Select(item => item.GetString()));
        var evidence = Assert.Single(persisted.Evidence);
        Assert.Equal(EvidenceSourceType.AI, evidence.SourceType);
        Assert.Equal(EvidenceTrustLevel.Trusted, evidence.TrustLevel);
        Assert.Equal(EvidenceDecisionMode.AIWeighted, evidence.DecisionMode);
        Assert.Equal(1m, evidence.ReasoningWeight);
        Assert.False(evidence.RequiresTeacherReview);
        Assert.Equal(analysis.AttemptId, evidence.AttemptId);
    }

    [Fact]
    public async Task ExecuteAsync_SkippedEmptyAnswerAndEmptyScoringNotes_StillCallsAIAndCompletes()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            retryCount: 0,
            skipped: true,
            finalAnswer: string.Empty,
            emptyScoringNotes: true);
        var aiService = new RecordingAIService((request, _) =>
        {
            Assert.Equal(string.Empty, request.StudentSubmission.FinalAnswer);
            Assert.Equal(string.Empty, request.Question.GradingCriteria.ScoringNotes);
            return Task.FromResult(ValidResponse("vi"));
        });

        var result = await ExecuteWithAIAsync(store, databaseName, centerId, aiService);

        Assert.Equal(AIAnalysisJobProcessingOutcome.Completed, result.Outcome);
        Assert.Equal(1, aiService.CallCount);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(AttemptStatus.Completed, persisted.Attempt.Status);
        Assert.Equal(AIJobStatus.Completed, persisted.Job.Status);
        Assert.Single(persisted.Analyses);
    }

    [Fact]
    public async Task ExecuteAsync_FirstProviderFailure_PersistsSingleSanitizedRetryWithoutAnalysis()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, retryCount: 0);
        var aiService = new RecordingAIService((_, _) =>
            throw new InvalidOperationException("raw-provider-sentinel-secret"));

        var result = await ExecuteWithAIAsync(
            store,
            databaseName,
            centerId,
            aiService);

        Assert.Equal(AIAnalysisJobProcessingOutcome.RetryScheduled, result.Outcome);
        Assert.Equal(1, aiService.CallCount);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(AttemptStatus.PendingAnalysis, persisted.Attempt.Status);
        Assert.Equal(AIJobStatus.Pending, persisted.Job.Status);
        Assert.Equal((byte)1, persisted.Job.RetryCount);
        Assert.Equal(UtcNow, persisted.Job.AvailableAt);
        Assert.Null(persisted.Job.StartedAt);
        Assert.Null(persisted.Job.CompletedAt);
        Assert.Null(persisted.Job.LeaseOwner);
        Assert.Null(persisted.Job.LeaseUntil);
        Assert.Equal("AI_ANALYSIS_ATTEMPT_FAILED", persisted.Job.LastErrorCode);
        Assert.Equal("AI analysis attempt failed.", persisted.Job.LastErrorMessage);
        Assert.DoesNotContain("sentinel", persisted.Job.LastErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(persisted.Analyses);
    }

    [Theory]
    [InlineData((byte)0, AIAnalysisJobProcessingOutcome.RetryScheduled)]
    [InlineData((byte)1, AIAnalysisJobProcessingOutcome.FallbackCompleted)]
    public async Task ExecuteAsync_InvalidRequestContext_UsesDurableFailurePolicyWithoutProvider(
        byte retryCount,
        AIAnalysisJobProcessingOutcome expectedOutcome)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            retryCount: retryCount,
            includeRequestContext: false);
        var aiService = new RecordingAIService((_, _) =>
            Task.FromResult(ValidResponse("vi")));

        var result = await ExecuteWithAIAsync(store, databaseName, centerId, aiService);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(0, aiService.CallCount);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal((byte)1, persisted.Job.RetryCount);
        Assert.Equal(
            retryCount == 0 ? AIJobStatus.Pending : AIJobStatus.FallbackCompleted,
            persisted.Job.Status);
        Assert.Equal(retryCount == 0 ? 0 : 1, persisted.Analyses.Count);
    }

    [Fact]
    public async Task ExecuteAsync_SecondProviderFailure_PersistsFallbackWithoutThirdRetry()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, retryCount: 1);
        var aiService = new RecordingAIService();

        var result = await ExecuteWithAIAsync(store, databaseName, centerId, aiService);

        Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted, result.Outcome);
        Assert.Equal(1, aiService.CallCount);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(AIJobStatus.FallbackCompleted, persisted.Job.Status);
        Assert.Equal((byte)1, persisted.Job.RetryCount);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, persisted.Attempt.Status);
        Assert.Equal("AI_ANALYSIS_ATTEMPT_FAILED", persisted.Job.LastErrorCode);
        Assert.Equal("AI analysis attempt failed.", persisted.Job.LastErrorMessage);
        var fallback = Assert.Single(persisted.Analyses);
        Assert.Null(fallback.ReasoningQuality);
        Assert.True(fallback.NeedsTeacherReview);
        Assert.Equal(AnalysisProvider.RuleBased, fallback.Provider);
        var evidence = Assert.Single(persisted.Evidence);
        Assert.Equal(EvidenceSourceType.RuleFallback, evidence.SourceType);
        Assert.Equal(EvidenceTrustLevel.ReviewOnly, evidence.TrustLevel);
        Assert.Equal(EvidenceDecisionMode.DeterministicOnly, evidence.DecisionMode);
        Assert.Equal(0m, evidence.ReasoningWeight);
        Assert.True(evidence.RequiresTeacherReview);
    }

    [Fact]
    public async Task ExecuteAsync_CallerCancellationDuringProvider_PropagatesWithoutMutation()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, retryCount: 0);
        using var cancellation = new CancellationTokenSource();
        var aiService = new RecordingAIService((_, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(ValidResponse("vi"));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExecuteWithAIAsync(
                store,
                databaseName,
                centerId,
                aiService,
                cancellation.Token));

        Assert.Equal(1, aiService.CallCount);
        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Theory]
    [InlineData("lease")]
    [InlineData("question")]
    [InlineData("mapping")]
    [InlineData("attempt")]
    public async Task ExecuteAsync_ContextOrAggregateDriftsDuringProvider_DiscardsResult(
        string driftKind)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, retryCount: 0);
        var aiService = new RecordingAIService(async (_, _) =>
        {
            await ApplyDriftAsync(store, databaseName, centerId, driftKind);
            return ValidResponse("vi");
        });

        var result = await ExecuteWithAIAsync(store, databaseName, centerId, aiService);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Empty(persisted.Analyses);
        Assert.NotEqual(AIJobStatus.Completed, persisted.Job.Status);
        Assert.NotEqual(AIJobStatus.FallbackCompleted, persisted.Job.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingAnalysis_PreventsProviderCall()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);
        await AddExistingAnalysisAsync(store, databaseName, centerId);
        var aiService = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));

        var result = await ExecuteWithAIAsync(store, databaseName, centerId, aiService);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        Assert.Equal(0, aiService.CallCount);
    }

    [Theory]
    [InlineData(AIJobStatus.Completed)]
    [InlineData(AIJobStatus.FallbackCompleted)]
    [InlineData(AIJobStatus.FailedTerminal)]
    public async Task ExecuteAsync_TerminalJob_PreventsProviderCall(AIJobStatus status)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, jobStatus: status);
        var aiService = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi")));

        var result = await ExecuteWithAIAsync(store, databaseName, centerId, aiService);

        Assert.Equal(AIAnalysisJobProcessingOutcome.AlreadyTerminal, result.Outcome);
        Assert.Equal(0, aiService.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ValidClaim_AtomicallyPersistsFallbackTerminalCheckpoint()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, assignmentId: assignmentId);

        var result = await ExecuteAsync(store, databaseName, centerId, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, persisted.Attempt.Status);
        Assert.Equal(UtcNow, persisted.Attempt.UpdatedAt);
        Assert.Equal(2ul, persisted.Attempt.RowVersion);
        Assert.Equal(AIJobStatus.FallbackCompleted, persisted.Job.Status);
        Assert.Equal(UtcNow, persisted.Job.CompletedAt);
        Assert.Null(persisted.Job.LeaseOwner);
        Assert.Null(persisted.Job.LeaseUntil);
        Assert.Equal((byte)1, persisted.Job.RetryCount);
        Assert.Equal(2ul, persisted.Job.RowVersion);
        var analysis = Assert.Single(persisted.Analyses);
        Assert.Equal("ai-analysis-v1", analysis.SchemaVersion);
        Assert.Null(analysis.ReasoningQuality);
        Assert.Null(analysis.AnalysisConfidence);
        Assert.Equal(ErrorType.Unknown, analysis.ErrorType);
        Assert.True(analysis.IsFallback);
        Assert.True(analysis.NeedsTeacherReview);
        Assert.Equal(AnalysisProvider.RuleBased, analysis.Provider);
        Assert.DoesNotContain("raw-answer-must-not-be-copied", analysis.Feedback);
        Assert.DoesNotContain("raw-reasoning-must-not-be-copied", analysis.Feedback);
        Assert.Null(analysis.CreatedBy);
        Assert.Equal(UtcNow, analysis.CreatedAt);
        Assert.NotNull(persisted.Progress);
        Assert.Equal(ProgressStatus.InProgress, persisted.Progress.Status);
        Assert.Equal(1u, persisted.Progress.CompletedQuestionCount);
        Assert.Equal(1ul, persisted.Progress.RowVersion);
        Assert.Equal(0, persisted.KnowledgeTwinCount);
        Assert.Equal(0, persisted.BehaviorTwinCount);
        Assert.Equal(0, persisted.HistoryCount);
        Assert.Equal(0, persisted.GoalCount);
        Assert.Equal(0, persisted.RecommendationCount);
        Assert.Equal(0, persisted.LearningPathCount);
    }

    [Fact]
    public async Task ExecuteAsync_LeaseEqualNow_IsStillValid()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, leaseUntil: UtcNow);

        var result = await ExecuteAsync(store, databaseName, centerId, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_AttemptAlreadyProcessing_IsAccepted()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            attemptStatus: AttemptStatus.Processing);

        var result = await ExecuteAsync(store, databaseName, centerId, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, persisted.Attempt.Status);
    }

    [Theory]
    [InlineData("wrong-owner")]
    [InlineData("")]
    public async Task ExecuteAsync_InvalidLeaseOwner_DoesNotMutate(string leaseOwner)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            leaseOwner: leaseOwner.Length == 0 ? null : leaseOwner);

        var result = await ExecuteAsync(store, databaseName, centerId, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_StrictlyExpiredLease_DoesNotMutate()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, leaseUntil: UtcNow.AddTicks(-1));

        var result = await ExecuteAsync(store, databaseName, centerId, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_NullLease_DoesNotMutate()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, nullLease: true);

        var result = await ExecuteAsync(store, databaseName, centerId, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_LeaseExpiresDuringPreflight_DoesNotOpenCompletionMutation()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            leaseUntil: UtcNow.AddSeconds(1));
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = new AIAnalysisJobProcessor(
            context,
            tenant,
            new RecordingAIService(),
            new AIAnalysisRequestFactory(),
            new AIReasoningAnalysisBuilder(),
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new SequenceTimeProvider(UtcNow, UtcNow.AddSeconds(2)));

        var result = await sut.ExecuteAsync(
            1,
            "worker-current",
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_LeaseExpiresAtTransactionalReload_DoesNotComplete()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            leaseUntil: UtcNow.AddSeconds(1));
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = new AIAnalysisJobProcessor(
            context,
            tenant,
            new RecordingAIService(),
            new AIAnalysisRequestFactory(),
            new AIReasoningAnalysisBuilder(),
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new SequenceTimeProvider(
                UtcNow,
                UtcNow,
                UtcNow.AddSeconds(2)));

        var result = await sut.ExecuteAsync(
            1,
            "worker-current",
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedOrAuthenticatedTenant_FailsClosed()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);

        var unresolved = new TenantContext();
        await using var unresolvedContext = CreateContext(store, databaseName, unresolved);
        var unresolvedResult = await CreateSut(unresolvedContext, unresolved, UtcNow)
            .ExecuteAsync(1, "worker-current", CancellationToken.None);

        var authenticated = new TenantContext();
        authenticated.Initialize(centerId, Guid.NewGuid(), "Student", 1);
        await using var authenticatedContext = CreateContext(store, databaseName, authenticated);
        var authenticatedResult = await CreateSut(authenticatedContext, authenticated, UtcNow)
            .ExecuteAsync(1, "worker-current", CancellationToken.None);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotFound, unresolvedResult.Outcome);
        Assert.Equal(AIAnalysisJobProcessingOutcome.NotFound, authenticatedResult.Outcome);
        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantScope_CannotReadOrMutateJob()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerA);

        var result = await ExecuteAsync(store, databaseName, centerB, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotFound, result.Outcome);
        await AssertUnchangedAsync(store, databaseName, centerA);
    }

    [Theory]
    [InlineData(AIJobStatus.Pending, AttemptStatus.PendingAnalysis)]
    [InlineData(AIJobStatus.Processing, AttemptStatus.Completed)]
    [InlineData(AIJobStatus.Processing, AttemptStatus.NeedsTeacherReview)]
    public async Task ExecuteAsync_IneligibleJobOrAttempt_DoesNotOverwrite(
        AIJobStatus jobStatus,
        AttemptStatus attemptStatus)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(
            store,
            databaseName,
            centerId,
            jobStatus: jobStatus,
            attemptStatus: attemptStatus);

        var result = await ExecuteAsync(store, databaseName, centerId, UtcNow);

        Assert.Equal(AIAnalysisJobProcessingOutcome.NotEligible, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(jobStatus, persisted.Job.Status);
        Assert.Equal(attemptStatus, persisted.Attempt.Status);
        Assert.Empty(persisted.Analyses);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatedAfterTerminal_ReturnsIdempotentNoOp()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);

        var first = await ExecuteAsync(store, databaseName, centerId, UtcNow);
        var second = await ExecuteAsync(store, databaseName, centerId, UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobProcessingOutcome.FallbackCompleted, first.Outcome);
        Assert.Equal(AIAnalysisJobProcessingOutcome.AlreadyTerminal, second.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Single(persisted.Analyses);
        Assert.Equal(AIJobStatus.FallbackCompleted, persisted.Job.Status);
    }

    [Fact]
    public async Task ExecuteAsync_TwoProcessorsCompete_ExactlyOneTransactionWins()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);
        var barrier = new DeterministicCompetingSaveInterceptor(2);
        var tenantA = new TenantContext();
        var tenantB = new TenantContext();
        using var scopeA = tenantA.BeginScope(centerId);
        using var scopeB = tenantB.BeginScope(centerId);
        await using var contextA = CreateContext(store, databaseName, tenantA, barrier);
        await using var contextB = CreateContext(store, databaseName, tenantB, barrier);

        var results = await Task.WhenAll(
            CreateSut(contextA, tenantA, UtcNow).ExecuteAsync(
                1,
                "worker-current",
                CancellationToken.None),
            CreateSut(contextB, tenantB, UtcNow).ExecuteAsync(
                1,
                "worker-current",
                CancellationToken.None));

        Assert.Single(
            results,
            result => result.Outcome == AIAnalysisJobProcessingOutcome.FallbackCompleted);
        Assert.Single(
            results,
            result => result.Outcome == AIAnalysisJobProcessingOutcome.LostRace);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Single(persisted.Analyses);
        Assert.Equal(AttemptStatus.NeedsTeacherReview, persisted.Attempt.Status);
        Assert.Equal(AIJobStatus.FallbackCompleted, persisted.Job.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentUniqueWinner_IsResolvedAsAlreadyTerminal()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);
        var interceptor = new ThrowingSaveInterceptor(
            async cancellationToken =>
            {
                var winningResult = await ExecuteAsync(
                    store,
                    databaseName,
                    centerId,
                    UtcNow,
                    cancellationToken: cancellationToken);
                Assert.Equal(
                    AIAnalysisJobProcessingOutcome.FallbackCompleted,
                    winningResult.Outcome);
            },
            new DbUpdateException("simulated unique conflict"));
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant, interceptor);

        var result = await CreateSut(context, tenant, UtcNow).ExecuteAsync(
            1,
            "worker-current",
            CancellationToken.None);

        Assert.Equal(AIAnalysisJobProcessingOutcome.AlreadyTerminal, result.Outcome);
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Single(persisted.Analyses);
    }

    [Fact]
    public async Task ExecuteAsync_UnrelatedDatabaseFailure_RollsBackAndPropagates()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(
            store,
            databaseName,
            tenant,
            new ThrowingSaveInterceptor(
                _ => Task.CompletedTask,
                new DbUpdateException("unrelated database failure")));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateSut(context, tenant, UtcNow).ExecuteAsync(
                1,
                "worker-current",
                CancellationToken.None));

        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_FallbackBuildFailure_HappensBeforeTransactionAndDoesNotMutate()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var sut = new AIAnalysisJobProcessor(
            context,
            tenant,
            new RecordingAIService(),
            new AIAnalysisRequestFactory(),
            new AIReasoningAnalysisBuilder(),
            new ThrowingFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new FixedTimeProvider(UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(
            1,
            "worker-current",
            CancellationToken.None));

        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelled_DoesNotQueryOrMutate()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut(context, tenant, UtcNow).ExecuteAsync(
                1,
                "worker-current",
                cancellation.Token));

        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringSave_RollsBackWithoutTerminalMutation()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId);
        using var cancellation = new CancellationTokenSource();
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(
            store,
            databaseName,
            tenant,
            new CancellingSaveInterceptor(cancellation));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut(context, tenant, UtcNow).ExecuteAsync(
                1,
                "worker-current",
                cancellation.Token));

        await AssertUnchangedAsync(store, databaseName, centerId);
    }

    private static async Task<AIAnalysisJobProcessingResult> ExecuteAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId,
        DateTime utcNow,
        string workerId = "worker-current",
        CancellationToken cancellationToken = default)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        return await CreateSut(context, tenant, utcNow).ExecuteAsync(
            1,
            workerId,
            cancellationToken);
    }

    private static async Task<AIAnalysisJobProcessingResult> ExecuteWithAIAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId,
        IAIService aiService,
        CancellationToken cancellationToken = default)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        return await CreateSut(context, tenant, UtcNow, aiService).ExecuteAsync(
            1,
            "worker-current",
            cancellationToken);
    }

    private static AnalyzeReasoningResponse ValidResponse(string language) => new()
    {
        SchemaVersion = AIAnalysisContract.SchemaVersion,
        Language = language,
        MethodDetected = "worked-example",
        ReasoningQuality = 84,
        ErrorType = ErrorType.Reasoning,
        Misconception = "missed transition",
        MissingSteps = ["show transition", "verify result"],
        RootCauseNodeIds = ["20"],
        Confidence = 91,
        Feedback = "Show the transition explicitly."
    };

    private static async Task ApplyDriftAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId,
        string driftKind)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        switch (driftKind)
        {
            case "lease":
                (await context.AIAnalysisJobs.SingleAsync()).LeaseOwner = "other-worker";
                break;
            case "question":
                (await context.Questions.SingleAsync()).QuestionText = "concurrently changed";
                break;
            case "mapping":
                context.QuestionKnowledgeNodes.Remove(
                    await context.QuestionKnowledgeNodes.SingleAsync(mapping =>
                        mapping.NodeId == 10
                        && mapping.MappingRole == MappingRole.Secondary));
                break;
            case "attempt":
                (await context.Attempts.SingleAsync()).ReasoningText = "concurrently changed";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(driftKind));
        }

        await context.SaveChangesAsync();
    }

    private static async Task AddExistingAnalysisAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        context.ReasoningAnalyses.Add(new RuleBasedFallbackBuilder().Build(
            new RuleBasedFallbackInput(
                centerId,
                1,
                true,
                1,
                false,
                "vi",
                UtcNow)));
        await context.SaveChangesAsync();
    }

    private static AIAnalysisJobProcessor CreateSut(
        EduTwinDbContext context,
        TenantContext tenant,
        DateTime utcNow,
        IAIService? aiService = null) =>
        new(
            context,
            tenant,
            aiService ?? new RecordingAIService(),
            new AIAnalysisRequestFactory(),
            new AIReasoningAnalysisBuilder(),
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
            new EvidenceGate(),
            new EvidenceAssessmentFactory(),
            new FixedTimeProvider(utcNow));

    private static EduTwinDbContext CreateContext(
        InMemoryDatabaseRoot store,
        string databaseName,
        TenantContext tenant,
        params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName, store)
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new EduTwinDbContext(builder.Options, tenant);
    }

    private static async Task SeedAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId,
        AIJobStatus jobStatus = AIJobStatus.Processing,
        AttemptStatus attemptStatus = AttemptStatus.PendingAnalysis,
        string? leaseOwner = "worker-current",
        DateTime? leaseUntil = null,
        bool nullLease = false,
        Guid? assignmentId = null,
        byte retryCount = 1,
        string language = "vi",
        bool includeRequestContext = true,
        bool skipped = false,
        string? finalAnswer = null,
        bool emptyScoringNotes = false)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        context.Attempts.Add(new Attempt
        {
            AttemptId = 1,
            CenterId = centerId,
            StudentId = Guid.NewGuid(),
            QuestionId = 10,
            AssignmentId = assignmentId,
            FinalAnswer = finalAnswer ?? "raw-answer-must-not-be-copied",
            ReasoningText = "raw-reasoning-must-not-be-copied",
            IsCorrect = true,
            AwardedScore = 1,
            TimeSpentSeconds = 30,
            Confidence = 80,
            AnswerChanges = 0,
            Skipped = skipped,
            ReasoningLanguage = language,
            Status = attemptStatus,
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = UtcNow.AddMinutes(-2),
            CreatedBy = Guid.NewGuid(),
            UpdatedAt = UtcNow.AddMinutes(-2)
        });
        context.AIAnalysisJobs.Add(new AIAnalysisJob
        {
            AnalysisJobId = 1,
            CenterId = centerId,
            AttemptId = 1,
            Status = jobStatus,
            RetryCount = retryCount,
            AvailableAt = UtcNow.AddMinutes(-2),
            StartedAt = jobStatus == AIJobStatus.Processing ? UtcNow.AddMinutes(-1) : null,
            LeaseOwner = jobStatus == AIJobStatus.Processing ? leaseOwner : null,
            LeaseUntil = jobStatus == AIJobStatus.Processing && !nullLease
                ? leaseUntil ?? UtcNow.AddMinutes(5)
                : null,
            CorrelationId = "processor-test",
            CreatedAt = UtcNow.AddMinutes(-2),
            UpdatedAt = UtcNow.AddMinutes(-1)
        });
        if (includeRequestContext)
        {
            AddRequestContext(context, centerId, language, emptyScoringNotes);
        }
        if (assignmentId.HasValue)
        {
            context.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
            {
                ProgressId = 1,
                CenterId = centerId,
                AssignmentId = assignmentId.Value,
                StudentId = context.Attempts.Local.Single().StudentId,
                Status = ProgressStatus.InProgress,
                CompletedQuestionCount = 1,
                TotalQuestionCount = 3,
                StartedAt = UtcNow.AddMinutes(-2),
                CreatedAt = UtcNow.AddMinutes(-2),
                UpdatedAt = UtcNow.AddMinutes(-2)
            });
        }

        await context.SaveChangesAsync();
    }

    private static void AddRequestContext(
        EduTwinDbContext context,
        Guid centerId,
        string language,
        bool emptyScoringNotes)
    {
        var subjectId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        context.Questions.Add(new Question
        {
            QuestionId = 10,
            CenterId = centerId,
            SubjectId = subjectId,
            PrimaryTopicNodeId = 20,
            CreatedByTeacherId = Guid.NewGuid(),
            QuestionType = QuestionType.Essay,
            Difficulty = 3,
            QuestionText = "Explain the solution.",
            CorrectAnswer = "42",
            Solution = "Apply the stated method.",
            ExpectedReasoning = "Show each transition.",
            GradingCriteria = new GradingCriteria
            {
                SchemaVersion = "1.0",
                RequiredIdeas = ["method", "result"],
                CommonErrors = ["sign"],
                ScoringNotes = emptyScoringNotes
                    ? string.Empty
                    : "Award for a valid method."
            },
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            ReasoningRequired = true,
            LanguageCode = language,
            Status = QuestionStatus.Active,
            CreatedAt = UtcNow.AddDays(-1),
            UpdatedAt = UtcNow.AddDays(-1)
        });

        context.KnowledgeNodes.AddRange(
            Node(10, centerId, subjectId, "Secondary mapped", true, false),
            Node(20, centerId, subjectId, "Primary mapped", true, false),
            Node(30, centerId, subjectId, "Inactive mapped", false, false),
            Node(40, centerId, subjectId, "Deleted mapped", true, true),
            Node(50, centerId, otherSubjectId, "Different subject mapped", true, false),
            Node(60, centerId, subjectId, "Unmapped", true, false),
            Node(70, otherCenterId, subjectId, "Cross tenant mapped", true, false));

        context.QuestionKnowledgeNodes.AddRange(
            Mapping(centerId, 10, 10, MappingRole.Secondary),
            Mapping(centerId, 10, 20, MappingRole.Primary),
            Mapping(centerId, 10, 20, MappingRole.Prerequisite),
            Mapping(centerId, 10, 30, MappingRole.Secondary),
            Mapping(centerId, 10, 40, MappingRole.Secondary),
            Mapping(centerId, 10, 50, MappingRole.Secondary),
            Mapping(otherCenterId, 10, 70, MappingRole.Secondary));

        static KnowledgeNode Node(
            ulong id,
            Guid nodeCenterId,
            Guid nodeSubjectId,
            string name,
            bool active,
            bool deleted) => new()
        {
            NodeId = id,
            CenterId = nodeCenterId,
            SubjectId = nodeSubjectId,
            NodeType = NodeType.Topic,
            NodeCode = $"NODE-{id}",
            NodeName = name,
            ExamImportance = 50,
            EstimatedLearningMinutes = 20,
            IsActive = active,
            IsDeleted = deleted,
            CreatedAt = UtcNow.AddDays(-1),
            UpdatedAt = UtcNow.AddDays(-1)
        };

        static QuestionKnowledgeNode Mapping(
            Guid mappingCenterId,
            ulong questionId,
            ulong nodeId,
            MappingRole role) => new()
        {
            CenterId = mappingCenterId,
            QuestionId = questionId,
            NodeId = nodeId,
            MappingRole = role,
            CreatedAt = UtcNow.AddDays(-1)
        };
    }

    private static async Task<PersistedState> ReloadAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId)
    {
        var tenant = new TenantContext();
        using var tenantScope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        return new PersistedState(
            await context.Attempts.AsNoTracking().SingleAsync(),
            await context.AIAnalysisJobs.AsNoTracking().SingleAsync(),
            await context.ReasoningAnalyses.AsNoTracking().ToArrayAsync(),
            await context.EvidenceAssessments.AsNoTracking().ToArrayAsync(),
            await context.StudentAssignmentProgresses.AsNoTracking().SingleOrDefaultAsync(),
            await context.KnowledgeTwins.CountAsync(),
            await context.BehaviorTwins.CountAsync(),
            await context.TwinUpdateHistories.CountAsync(),
            await context.StudentSubjectGoals.CountAsync(),
            await context.Recommendations.CountAsync(),
            await context.LearningPaths.CountAsync());
    }

    private static async Task AssertUnchangedAsync(
        InMemoryDatabaseRoot store,
        string databaseName,
        Guid centerId)
    {
        var persisted = await ReloadAsync(store, databaseName, centerId);
        Assert.Equal(AttemptStatus.PendingAnalysis, persisted.Attempt.Status);
        Assert.Equal(1ul, persisted.Attempt.RowVersion);
        Assert.Equal(AIJobStatus.Processing, persisted.Job.Status);
        Assert.Equal(1ul, persisted.Job.RowVersion);
        Assert.Empty(persisted.Analyses);
    }

    private sealed record PersistedState(
        Attempt Attempt,
        AIAnalysisJob Job,
        IReadOnlyList<ReasoningAnalysis> Analyses,
        IReadOnlyList<EvidenceAssessment> Evidence,
        StudentAssignmentProgress? Progress,
        int KnowledgeTwinCount,
        int BehaviorTwinCount,
        int HistoryCount,
        int GoalCount,
        int RecommendationCount,
        int LearningPathCount);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class SequenceTimeProvider(params DateTime[] utcValues) : TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow()
        {
            var callIndex = Interlocked.Increment(ref _calls) - 1;
            var valueIndex = Math.Min(callIndex, utcValues.Length - 1);
            return new DateTimeOffset(utcValues[valueIndex]);
        }
    }

    private sealed class DeterministicCompetingSaveInterceptor(int participantCount) :
        SaveChangesInterceptor
    {
        private int _remaining = participantCount;
        private int _winnerSelected;
        private readonly TaskCompletionSource _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _winnerSaved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
            {
                _allArrived.TrySetResult();
            }

            await _allArrived.Task.WaitAsync(cancellationToken);
            if (Interlocked.CompareExchange(ref _winnerSelected, 1, 0) == 0)
            {
                return result;
            }

            await _winnerSaved.Task.WaitAsync(cancellationToken);
            throw new DbUpdateConcurrencyException("Simulated persisted row-version race.");
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            _winnerSaved.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingSaveInterceptor(
        Func<CancellationToken, Task> beforeThrow,
        Exception exception) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await beforeThrow(cancellationToken);
            throw exception;
        }
    }

    private sealed class CancellingSaveInterceptor(CancellationTokenSource source) :
        SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingFallbackBuilder : IRuleBasedFallbackBuilder
    {
        public ReasoningAnalysis Build(RuleBasedFallbackInput input) =>
            throw new InvalidOperationException("Deliberate fallback build failure.");
    }

    private sealed class RecordingAIService : IAIService
    {
        private readonly Func<AnalyzeReasoningRequest, CancellationToken, Task<AnalyzeReasoningResponse>>
            _handler;

        public RecordingAIService(
            Func<AnalyzeReasoningRequest, CancellationToken, Task<AnalyzeReasoningResponse>>?
                handler = null)
        {
            _handler = handler ?? ((_, _) =>
                throw new InvalidOperationException("Deliberate AI analysis failure."));
        }

        public int CallCount { get; private set; }
        public AnalyzeReasoningRequest? Request { get; private set; }

        public Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
            AnalyzeReasoningRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Request = request;
            return _handler(request, cancellationToken);
        }
    }
}
