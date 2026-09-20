using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.Retry;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Retry;

public sealed class RetryAttemptAIAnalysisUseCaseTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IStudentOwnershipGuard> _guard = new();
    private readonly Mock<TimeProvider> _timeProvider = new();

    public RetryAttemptAIAnalysisUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContext.SetupGet(t => t.IsResolved).Returns(true);
        _tenantContext.SetupGet(t => t.CenterId).Returns(_centerId);
        _tenantContext.SetupGet(t => t.UserId).Returns(_studentId);
        _tenantContext.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));

        _guard.Setup(g => g.CheckStudentAccessAsync(_studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        _timeProvider.Setup(p => p.GetUtcNow()).Returns(FixedNow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobIsProcessing_ReturnsJobProcessing()
    {
        await using var context = CreateContext();
        var (attempt, job) = await SeedAttemptWithJobAsync(context, AIJobStatus.Processing);

        var sut = new RetryAttemptAIAnalysisUseCase(context, _tenantContext.Object, _guard.Object, _timeProvider.Object);
        var result = await sut.ExecuteAsync(attempt.AttemptId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("JOB_PROCESSING", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobIsPending_ReturnsJobProcessing()
    {
        await using var context = CreateContext();
        var (attempt, job) = await SeedAttemptWithJobAsync(context, AIJobStatus.Pending);

        var sut = new RetryAttemptAIAnalysisUseCase(context, _tenantContext.Object, _guard.Object, _timeProvider.Object);
        var result = await sut.ExecuteAsync(attempt.AttemptId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("JOB_PROCESSING", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobIsCompleted_ReturnsJobAlreadyCompleted()
    {
        await using var context = CreateContext();
        var (attempt, job) = await SeedAttemptWithJobAsync(context, AIJobStatus.Completed);

        var sut = new RetryAttemptAIAnalysisUseCase(context, _tenantContext.Object, _guard.Object, _timeProvider.Object);
        var result = await sut.ExecuteAsync(attempt.AttemptId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("JOB_ALREADY_COMPLETED", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobIsFailedTerminal_ReusesExistingAttemptAndResetsJob()
    {
        await using var context = CreateContext();
        var (attempt, job) = await SeedAttemptWithJobAsync(context, AIJobStatus.FailedTerminal);
        var originalAnswer = attempt.FinalAnswer;
        var originalReasoning = attempt.ReasoningText;
        var originalConfidence = attempt.Confidence;

        var sut = new RetryAttemptAIAnalysisUseCase(context, _tenantContext.Object, _guard.Object, _timeProvider.Object);
        var result = await sut.ExecuteAsync(attempt.AttemptId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(attempt.AttemptId.ToString(), result.Data.AttemptId);

        // Verify attempt was reused, not duplicated
        var allAttempts = await context.Attempts.ToListAsync();
        Assert.Single(allAttempts);
        var refreshedAttempt = allAttempts[0];
        Assert.Equal(attempt.AttemptId, refreshedAttempt.AttemptId);
        Assert.Equal(originalAnswer, refreshedAttempt.FinalAnswer);
        Assert.Equal(originalReasoning, refreshedAttempt.ReasoningText);
        Assert.Equal(originalConfidence, refreshedAttempt.Confidence);
        Assert.Equal(AttemptStatus.PendingAnalysis, refreshedAttempt.Status);
        Assert.Equal(1, refreshedAttempt.ManualRetryCount);

        // Verify job reset to Pending
        var refreshedJob = await context.AIAnalysisJobs.SingleAsync();
        Assert.Equal(AIJobStatus.Pending, refreshedJob.Status);
        Assert.Equal(0, refreshedJob.RetryCount);
    }

    private async Task<(Attempt Attempt, AIAnalysisJob Job)> SeedAttemptWithJobAsync(
        EduTwinDbContext context,
        AIJobStatus jobStatus)
    {
        var attempt = new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 101,
            FinalAnswer = "4",
            ReasoningText = "2 plus 2 equals 4",
            Confidence = 90,
            TimeSpentSeconds = 30,
            AnswerChanges = 0,
            Status = jobStatus == AIJobStatus.Completed ? AttemptStatus.Completed : AttemptStatus.PendingAnalysis,
            ReasoningLanguage = "vi",
            ManualRetryCount = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedBy = _studentId,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.Attempts.Add(attempt);
        await context.SaveChangesAsync();

        var job = new AIAnalysisJob
        {
            CenterId = _centerId,
            AttemptId = attempt.AttemptId,
            Status = jobStatus,
            RetryCount = 0,
            AvailableAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedBy = _studentId,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        context.AIAnalysisJobs.Add(job);
        await context.SaveChangesAsync();

        return (attempt, job);
    }

    private EduTwinDbContext CreateContext()
    {
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(candidate => candidate.CenterId).Returns(_centerId);
        return new EduTwinDbContext(_options, accessor.Object);
    }
}
