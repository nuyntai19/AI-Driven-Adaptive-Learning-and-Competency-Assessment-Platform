using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

public sealed class SubmitAttemptUseCaseTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 15, 8, 30, 45, 123, TimeSpan.Zero);

    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<IAttemptSubmissionValidator> _validator = new();
    private readonly Mock<TimeProvider> _timeProvider = new();

    public SubmitAttemptUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _timeProvider.Setup(provider => provider.GetUtcNow()).Returns(FixedNow);
    }

    [Fact]
    public async Task ExecuteAsync_FreePractice_CreatesAttemptAndJobWithoutProgress()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-001");

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Data);
        Assert.Equal("PendingAnalysis", result.Data.AttemptStatus);
        Assert.Equal("Pending", result.Data.JobStatus);
        Assert.Equal(3000, result.Data.PollAfterMilliseconds);
        Assert.Equal(
            $"/api/v1/learning/analysis-jobs/{result.Data.AnalysisJobId}",
            result.Data.PollUrl);
        Assert.Single(await context.Attempts.ToListAsync());
        Assert.Single(await context.AIAnalysisJobs.ToListAsync());
        Assert.Empty(await context.StudentAssignmentProgresses.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_AssignmentMovesNotStartedProgressAndPreservesCounts()
    {
        await using var context = CreateContext(_centerId);
        var assignmentId = Guid.NewGuid();
        var progress = CreateProgress(assignmentId, ProgressStatus.NotStarted);
        progress.CompletedQuestionCount = 2;
        progress.TotalQuestionCount = 7;
        var originalCreatedAt = progress.CreatedAt;
        var originalCreatedBy = progress.CreatedBy;
        context.StudentAssignmentProgresses.Add(progress);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var submission = CreateSubmission(assignmentId);
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-assignment");

        Assert.True(result.IsSuccess);
        var persisted = await context.StudentAssignmentProgresses.SingleAsync();
        Assert.Equal(ProgressStatus.InProgress, persisted.Status);
        Assert.Equal(FixedNow.UtcDateTime, persisted.StartedAt);
        Assert.Equal(FixedNow.UtcDateTime, persisted.UpdatedAt);
        Assert.Equal(_studentId, persisted.UpdatedBy);
        Assert.Equal(2u, persisted.CompletedQuestionCount);
        Assert.Equal(7u, persisted.TotalQuestionCount);
        Assert.Equal(originalCreatedAt, persisted.CreatedAt);
        Assert.Equal(originalCreatedBy, persisted.CreatedBy);
        Assert.Equal(2ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_InProgressProgressDoesNotResetStartedAtOrAudit()
    {
        await using var context = CreateContext(_centerId);
        var assignmentId = Guid.NewGuid();
        var progress = CreateProgress(assignmentId, ProgressStatus.InProgress);
        var originalStartedAt = new DateTime(2026, 7, 10, 2, 0, 0, DateTimeKind.Utc);
        var originalUpdatedAt = new DateTime(2026, 7, 10, 2, 1, 0, DateTimeKind.Utc);
        var originalUpdater = Guid.NewGuid();
        progress.StartedAt = originalStartedAt;
        progress.UpdatedAt = originalUpdatedAt;
        progress.UpdatedBy = originalUpdater;
        context.StudentAssignmentProgresses.Add(progress);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var submission = CreateSubmission(assignmentId);
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-progress");

        Assert.True(result.IsSuccess);
        var persisted = await context.StudentAssignmentProgresses.SingleAsync();
        Assert.Equal(ProgressStatus.InProgress, persisted.Status);
        Assert.Equal(originalStartedAt, persisted.StartedAt);
        Assert.Equal(originalUpdatedAt, persisted.UpdatedAt);
        Assert.Equal(originalUpdater, persisted.UpdatedBy);
        Assert.Equal(1ul, persisted.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsPreliminaryGradeAndSubmissionMetrics()
    {
        await using var context = CreateContext(_centerId);
        var submission = CloneSubmission(
            CreateSubmission(),
            isCorrect: true,
            awardedScore: 8.75m);
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        await CreateSut(context).ExecuteAsync(request, "trace-grade");

        var attempt = await context.Attempts.SingleAsync();
        Assert.True(attempt.IsCorrect);
        Assert.Equal(8.75m, attempt.AwardedScore);
        Assert.Equal(submission.FinalAnswer, attempt.FinalAnswer);
        Assert.Equal(submission.ReasoningText, attempt.ReasoningText);
        Assert.Equal(submission.TimeSpentSeconds, attempt.TimeSpentSeconds);
        Assert.Equal(submission.Confidence, attempt.Confidence);
        Assert.Equal(submission.AnswerChanges, attempt.AnswerChanges);
        Assert.Equal(submission.Skipped, attempt.Skipped);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsTenantAuditStatusAndSanitizedCorrelation()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var request = CreateRequest(submission);
        SetupValidation(request, submission);
        var correlation = "  trace\r\n-" + new string('x', 80);

        await CreateSut(context).ExecuteAsync(request, correlation);

        var attempt = await context.Attempts.SingleAsync();
        var job = await context.AIAnalysisJobs.SingleAsync();
        Assert.Equal(_centerId, attempt.CenterId);
        Assert.Equal(_studentId, attempt.StudentId);
        Assert.Equal(AttemptStatus.PendingAnalysis, attempt.Status);
        Assert.Equal(FixedNow.UtcDateTime, attempt.CreatedAt);
        Assert.Equal(FixedNow.UtcDateTime, attempt.UpdatedAt);
        Assert.Equal(_studentId, attempt.CreatedBy);
        Assert.Equal(1ul, attempt.RowVersion);
        Assert.Equal(_centerId, job.CenterId);
        Assert.Equal(attempt.AttemptId, job.AttemptId);
        Assert.Equal(AIJobStatus.Pending, job.Status);
        Assert.Equal((byte)0, job.RetryCount);
        Assert.Equal(FixedNow.UtcDateTime, job.AvailableAt);
        Assert.Equal(FixedNow.UtcDateTime, job.CreatedAt);
        Assert.Equal(FixedNow.UtcDateTime, job.UpdatedAt);
        Assert.Equal(_studentId, job.CreatedBy);
        Assert.Equal(1ul, job.RowVersion);
        Assert.DoesNotContain('\r', job.CorrelationId);
        Assert.DoesNotContain('\n', job.CorrelationId);
        Assert.Equal(64, job.CorrelationId.Length);
    }

    [Fact]
    public async Task ExecuteAsync_SamePayloadReplayReturnsExistingIdsWithoutWriting()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var (attempt, job) = await SeedAttemptAndJobAsync(context, submission);
        var replay = CloneAsReplay(submission, attempt.AttemptId);
        var request = CreateRequest(submission);
        SetupValidation(request, replay);
        context.ChangeTracker.Clear();

        var result = await CreateSut(context).ExecuteAsync(request, "new-correlation");

        Assert.True(result.IsSuccess);
        Assert.Equal(attempt.AttemptId.ToString(), result.Data!.AttemptId);
        Assert.Equal(job.AnalysisJobId.ToString(), result.Data.AnalysisJobId);
        Assert.Single(await context.Attempts.ToListAsync());
        Assert.Single(await context.AIAnalysisJobs.ToListAsync());
        var persistedJob = await context.AIAnalysisJobs.SingleAsync();
        Assert.Equal("original-correlation", persistedJob.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_ReplayReturnsCurrentStatusesWithoutResettingJob()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var (attempt, job) = await SeedAttemptAndJobAsync(
            context,
            submission,
            AttemptStatus.Processing,
            AIJobStatus.FailedTerminal);
        job.RetryCount = 1;
        job.LastErrorCode = "ANALYSIS_FAILED";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var request = CreateRequest(submission);
        SetupValidation(request, CloneAsReplay(submission, attempt.AttemptId));

        var result = await CreateSut(context).ExecuteAsync(request, "replacement");

        Assert.Equal("Processing", result.Data!.AttemptStatus);
        Assert.Equal("FailedTerminal", result.Data.JobStatus);
        var persistedJob = await context.AIAnalysisJobs.SingleAsync();
        Assert.Equal((byte)1, persistedJob.RetryCount);
        Assert.Equal("ANALYSIS_FAILED", persistedJob.LastErrorCode);
        Assert.Equal("original-correlation", persistedJob.CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_ReplayReadPathLeavesChangeTrackerEmpty()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var (attempt, _) = await SeedAttemptAndJobAsync(context, submission);
        context.ChangeTracker.Clear();
        var request = CreateRequest(submission);
        SetupValidation(request, CloneAsReplay(submission, attempt.AttemptId));

        await CreateSut(context).ExecuteAsync(request, "trace-replay");

        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExecuteAsync_ReplayWithoutJobFailsClosedAndDoesNotRepair()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var attempt = CreateAttempt(submission, 12001);
        context.Attempts.Add(attempt);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var request = CreateRequest(submission);
        SetupValidation(request, CloneAsReplay(submission, attempt.AttemptId));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateSut(context).ExecuteAsync(request, "trace-missing-job"));

        Assert.Empty(await context.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_TransactionBoundaryRecheckReturnsExistingPair()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var (attempt, job) = await SeedAttemptAndJobAsync(context, submission);
        context.ChangeTracker.Clear();
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-boundary");

        Assert.Equal(attempt.AttemptId.ToString(), result.Data!.AttemptId);
        Assert.Equal(job.AnalysisJobId.ToString(), result.Data.AnalysisJobId);
        Assert.Single(await context.Attempts.ToListAsync());
        Assert.Single(await context.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_TransactionBoundaryChangedPayloadReturnsDuplicate()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var persisted = CloneSubmission(submission, finalAnswer: "different");
        await SeedAttemptAndJobAsync(context, persisted);
        context.ChangeTracker.Clear();
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-boundary");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateSubmission, result.ErrorCode);
        Assert.Single(await context.Attempts.ToListAsync());
        Assert.Single(await context.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_ChangedPayloadValidationFailureDoesNotPersist()
    {
        await using var context = CreateContext(_centerId);
        var request = CreateRequest(CreateSubmission());
        _validator.Setup(candidate => candidate.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AttemptSubmissionValidationResult.Failure(ErrorCodes.DuplicateSubmission));

        var result = await CreateSut(context).ExecuteAsync(request, "trace-duplicate");

        Assert.Equal(ErrorCodes.DuplicateSubmission, result.ErrorCode);
        Assert.Empty(await context.Attempts.ToListAsync());
        Assert.Empty(await context.AIAnalysisJobs.ToListAsync());
    }

    [Theory]
    [InlineData(ErrorCodes.ValidationFailed)]
    [InlineData(ErrorCodes.ResourceNotFound)]
    [InlineData(ErrorCodes.DuplicateSubmission)]
    [InlineData(ErrorCodes.AssignmentNotAvailable)]
    [InlineData(ErrorCodes.QuestionReasoningRequired)]
    public async Task ExecuteAsync_ValidatorFailurePropagatesWithoutPersistence(string errorCode)
    {
        await using var context = CreateContext(_centerId);
        var request = CreateRequest(CreateSubmission());
        _validator.Setup(candidate => candidate.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AttemptSubmissionValidationResult.Failure(errorCode));

        var result = await CreateSut(context).ExecuteAsync(request, "trace-validation");

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Empty(await context.Attempts.ToListAsync());
        Assert.Empty(await context.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_MissingProgressReturnsAssignmentUnavailableWithoutHalfPersist()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission(Guid.NewGuid());
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-progress");

        Assert.Equal(ErrorCodes.AssignmentNotAvailable, result.ErrorCode);
        Assert.Empty(await context.Attempts.ToListAsync());
        Assert.Empty(await context.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantProgressIsHiddenAndDoesNotHalfPersist()
    {
        await using var context = CreateContext(_centerId);
        var assignmentId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var crossTenantProgress = CreateProgress(assignmentId, ProgressStatus.NotStarted);
        crossTenantProgress.CenterId = otherCenterId;
        context.StudentAssignmentProgresses.Add(crossTenantProgress);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Single(await context.StudentAssignmentProgresses.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.StudentAssignmentProgresses.ToListAsync());

        var submission = CreateSubmission(assignmentId);
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-cross-tenant");

        Assert.Equal(ErrorCodes.AssignmentNotAvailable, result.ErrorCode);
        Assert.Empty(await context.Attempts.ToListAsync());
        Assert.Empty(await context.AIAnalysisJobs.ToListAsync());
        Assert.Equal(
            ProgressStatus.NotStarted,
            (await context.StudentAssignmentProgresses.IgnoreQueryFilters().SingleAsync()).Status);
    }

    [Fact]
    public async Task ExecuteAsync_DeletedProgressIsHiddenAndDoesNotHalfPersist()
    {
        await using var context = CreateContext(_centerId);
        var assignmentId = Guid.NewGuid();
        var deletedProgress = CreateProgress(assignmentId, ProgressStatus.NotStarted);
        deletedProgress.IsDeleted = true;
        deletedProgress.DeletedAt = FixedNow.UtcDateTime.AddDays(-1);
        deletedProgress.DeletedBy = Guid.NewGuid();
        context.StudentAssignmentProgresses.Add(deletedProgress);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.Single(await context.StudentAssignmentProgresses.IgnoreQueryFilters().ToListAsync());

        var submission = CreateSubmission(assignmentId);
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var result = await CreateSut(context).ExecuteAsync(request, "trace-deleted");

        Assert.Equal(ErrorCodes.AssignmentNotAvailable, result.ErrorCode);
        Assert.Empty(await context.Attempts.ToListAsync());
        Assert.Empty(await context.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_CancellationBeforeTransactionPropagatesWithoutPersistence()
    {
        await using var context = CreateContext(_centerId);
        var submission = CreateSubmission();
        var request = CreateRequest(submission);
        SetupValidation(request, submission);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut(context).ExecuteAsync(request, "trace-cancel", cancellation.Token));

        Assert.Empty(await context.Attempts.ToListAsync());
        Assert.Empty(await context.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringSaveRollsBackAndPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        await using var context = CreateFaultingContext(
            _centerId,
            token =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<int>(token);
            });
        var submission = CreateSubmission();
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut(context).ExecuteAsync(request, "trace-cancel-save", cancellation.Token));

        Assert.Empty(context.Attempts.Local);
        Assert.Empty(context.AIAnalysisJobs.Local);
    }

    [Fact]
    public async Task ExecuteAsync_UnrelatedDatabaseExceptionRollsBackAndPropagates()
    {
        var databaseException = new DbUpdateException("database connection failed");
        await using var context = CreateFaultingContext(
            _centerId,
            _ => Task.FromException<int>(databaseException));
        var submission = CreateSubmission();
        var request = CreateRequest(submission);
        SetupValidation(request, submission);

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateSut(context).ExecuteAsync(request, "trace-database"));

        Assert.Same(databaseException, thrown);
        Assert.Empty(context.Attempts.Local);
        Assert.Empty(context.AIAnalysisJobs.Local);
    }

    [Fact]
    public void AddAssessmentAndReasoning_ResolvesValidatorAndUseCaseInScope()
    {
        var tenantContext = new Mock<ITenantContext>();
        var tenantAccessor = new Mock<ITenantIdAccessor>();
        tenantAccessor.SetupGet(accessor => accessor.CenterId).Returns(_centerId);
        var services = new ServiceCollection();
        services.AddSingleton(tenantContext.Object);
        services.AddSingleton(tenantAccessor.Object);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<EduTwinDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddAssessmentAndReasoning();

        var validatorDescriptor = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IAttemptSubmissionValidator));
        Assert.Equal(ServiceLifetime.Scoped, validatorDescriptor.Lifetime);
        var useCaseDescriptor = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(ISubmitAttemptUseCase));
        Assert.Equal(ServiceLifetime.Scoped, useCaseDescriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<AttemptSubmissionValidator>(
            scope.ServiceProvider.GetRequiredService<IAttemptSubmissionValidator>());
        Assert.IsType<SubmitAttemptUseCase>(
            scope.ServiceProvider.GetRequiredService<ISubmitAttemptUseCase>());
    }

    // EF Core InMemory does not enforce the two MySQL unique indexes or reproduce
    // concurrent transaction timing. The production path verifies the named Attempt
    // unique constraint, rolls back, reloads the committed row, and only then maps it.

    private SubmitAttemptUseCase CreateSut(EduTwinDbContext context) =>
        new(context, _validator.Object, _timeProvider.Object);

    private EduTwinDbContext CreateContext(Guid tenantId)
    {
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(candidate => candidate.CenterId).Returns(tenantId);
        return new EduTwinDbContext(_options, accessor.Object);
    }

    private FaultingEduTwinDbContext CreateFaultingContext(
        Guid tenantId,
        Func<CancellationToken, Task<int>> saveChanges)
    {
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(candidate => candidate.CenterId).Returns(tenantId);
        return new FaultingEduTwinDbContext(_options, accessor.Object, saveChanges);
    }

    private void SetupValidation(
        SubmitAttemptRequest request,
        ValidatedAttemptSubmission submission) =>
        _validator
            .Setup(candidate => candidate.ValidateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AttemptSubmissionValidationResult.Success(submission));

    private ValidatedAttemptSubmission CreateSubmission(Guid? assignmentId = null) =>
        new()
        {
            CenterId = _centerId,
            StudentId = _studentId,
            ClientSubmissionId = Guid.NewGuid(),
            QuestionId = 9001,
            AssignmentId = assignmentId,
            FinalAnswer = "B",
            ReasoningText = "Lập luận kiểm tra",
            TimeSpentSeconds = 165,
            Confidence = 80,
            AnswerChanges = 1,
            Skipped = false,
            ReasoningLanguage = "vi",
            IsCorrect = false,
            AwardedScore = 0m
        };

    private static SubmitAttemptRequest CreateRequest(ValidatedAttemptSubmission submission) =>
        new()
        {
            ClientSubmissionId = submission.ClientSubmissionId,
            QuestionId = submission.QuestionId.ToString(),
            AssignmentId = submission.AssignmentId,
            FinalAnswer = submission.FinalAnswer,
            ReasoningText = submission.ReasoningText,
            TimeSpentSeconds = submission.TimeSpentSeconds,
            Confidence = submission.Confidence,
            AnswerChanges = submission.AnswerChanges,
            Skipped = submission.Skipped
        };

    private StudentAssignmentProgress CreateProgress(
        Guid assignmentId,
        ProgressStatus status) =>
        new()
        {
            ProgressId = 5001,
            CenterId = _centerId,
            AssignmentId = assignmentId,
            StudentId = _studentId,
            Status = status,
            CompletedQuestionCount = 0,
            TotalQuestionCount = 5,
            CreatedAt = FixedNow.UtcDateTime.AddDays(-2),
            CreatedBy = Guid.NewGuid(),
            UpdatedAt = FixedNow.UtcDateTime.AddDays(-2),
            UpdatedBy = Guid.NewGuid(),
            IsDeleted = false,
            RowVersion = 1
        };

    private static Attempt CreateAttempt(
        ValidatedAttemptSubmission submission,
        ulong attemptId,
        AttemptStatus status = AttemptStatus.PendingAnalysis) =>
        new()
        {
            AttemptId = attemptId,
            CenterId = submission.CenterId,
            StudentId = submission.StudentId,
            QuestionId = submission.QuestionId,
            AssignmentId = submission.AssignmentId,
            FinalAnswer = submission.FinalAnswer,
            ReasoningText = submission.ReasoningText,
            IsCorrect = submission.IsCorrect,
            AwardedScore = submission.AwardedScore,
            TimeSpentSeconds = submission.TimeSpentSeconds,
            Confidence = submission.Confidence,
            AnswerChanges = submission.AnswerChanges,
            Skipped = submission.Skipped,
            ReasoningLanguage = submission.ReasoningLanguage,
            Status = status,
            ClientSubmissionId = submission.ClientSubmissionId,
            CreatedAt = FixedNow.UtcDateTime,
            CreatedBy = submission.StudentId,
            UpdatedAt = FixedNow.UtcDateTime,
            RowVersion = 1
        };

    private static async Task<(Attempt Attempt, AIAnalysisJob Job)> SeedAttemptAndJobAsync(
        EduTwinDbContext context,
        ValidatedAttemptSubmission submission,
        AttemptStatus attemptStatus = AttemptStatus.PendingAnalysis,
        AIJobStatus jobStatus = AIJobStatus.Pending)
    {
        var attempt = CreateAttempt(submission, 12001, attemptStatus);
        var job = new AIAnalysisJob
        {
            AnalysisJobId = 13001,
            CenterId = submission.CenterId,
            Attempt = attempt,
            Status = jobStatus,
            RetryCount = 0,
            AvailableAt = FixedNow.UtcDateTime,
            CorrelationId = "original-correlation",
            CreatedAt = FixedNow.UtcDateTime,
            CreatedBy = submission.StudentId,
            UpdatedAt = FixedNow.UtcDateTime,
            RowVersion = 1
        };
        context.Attempts.Add(attempt);
        context.AIAnalysisJobs.Add(job);
        await context.SaveChangesAsync();
        return (attempt, job);
    }

    private static ValidatedAttemptSubmission CloneAsReplay(
        ValidatedAttemptSubmission submission,
        ulong attemptId) =>
        new()
        {
            CenterId = submission.CenterId,
            StudentId = submission.StudentId,
            ClientSubmissionId = submission.ClientSubmissionId,
            QuestionId = submission.QuestionId,
            AssignmentId = submission.AssignmentId,
            FinalAnswer = submission.FinalAnswer,
            ReasoningText = submission.ReasoningText,
            TimeSpentSeconds = submission.TimeSpentSeconds,
            Confidence = submission.Confidence,
            AnswerChanges = submission.AnswerChanges,
            Skipped = submission.Skipped,
            ReasoningLanguage = submission.ReasoningLanguage,
            IsCorrect = submission.IsCorrect,
            AwardedScore = submission.AwardedScore,
            ExistingAttemptId = attemptId
        };

    private static ValidatedAttemptSubmission CloneSubmission(
        ValidatedAttemptSubmission submission,
        string? finalAnswer = null,
        bool? isCorrect = null,
        decimal? awardedScore = null) =>
        new()
        {
            CenterId = submission.CenterId,
            StudentId = submission.StudentId,
            ClientSubmissionId = submission.ClientSubmissionId,
            QuestionId = submission.QuestionId,
            AssignmentId = submission.AssignmentId,
            FinalAnswer = finalAnswer ?? submission.FinalAnswer,
            ReasoningText = submission.ReasoningText,
            TimeSpentSeconds = submission.TimeSpentSeconds,
            Confidence = submission.Confidence,
            AnswerChanges = submission.AnswerChanges,
            Skipped = submission.Skipped,
            ReasoningLanguage = submission.ReasoningLanguage,
            IsCorrect = isCorrect ?? submission.IsCorrect,
            AwardedScore = awardedScore ?? submission.AwardedScore,
            ExistingAttemptId = submission.ExistingAttemptId
        };

    private sealed class FaultingEduTwinDbContext : EduTwinDbContext
    {
        private readonly Func<CancellationToken, Task<int>> _saveChanges;

        public FaultingEduTwinDbContext(
            DbContextOptions<EduTwinDbContext> options,
            ITenantIdAccessor tenantIdAccessor,
            Func<CancellationToken, Task<int>> saveChanges)
            : base(options, tenantIdAccessor)
        {
            _saveChanges = saveChanges;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _saveChanges(cancellationToken);
    }
}
