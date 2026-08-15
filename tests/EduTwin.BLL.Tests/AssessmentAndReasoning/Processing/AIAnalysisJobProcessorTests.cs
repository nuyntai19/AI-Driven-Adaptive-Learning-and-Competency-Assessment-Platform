using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
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
        Assert.Equal((byte)0, persisted.Job.RetryCount);
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
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
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
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
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
            new ThrowingFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
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

    private static AIAnalysisJobProcessor CreateSut(
        EduTwinDbContext context,
        TenantContext tenant,
        DateTime utcNow) =>
        new(
            context,
            tenant,
            new RuleBasedFallbackBuilder(),
            new AIAnalysisJobStateMachine(),
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
        Guid? assignmentId = null)
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
            FinalAnswer = "raw-answer-must-not-be-copied",
            ReasoningText = "raw-reasoning-must-not-be-copied",
            IsCorrect = true,
            AwardedScore = 1,
            TimeSpentSeconds = 30,
            Confidence = 80,
            AnswerChanges = 0,
            Skipped = false,
            ReasoningLanguage = "vi",
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
            RetryCount = 0,
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
}
