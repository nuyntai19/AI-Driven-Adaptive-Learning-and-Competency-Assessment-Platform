using EduTwin.BLL.AssessmentAndReasoning.Polling;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Polling;

public sealed class GetAnalysisJobStatusUseCaseTests
{
    private static readonly DateTime JobUpdatedAt =
        new(2026, 8, 15, 10, 20, 30, DateTimeKind.Utc);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("12a")]
    [InlineData("18446744073709551616")]
    public async Task ExecuteAsync_InvalidRouteId_ReturnsValidationFailed(
        string? routeId)
    {
        var fixture = CreateFixture(UserRole.Student);

        var result = await fixture.UseCase.ExecuteAsync(
            routeId!,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Null(result.Data);
        fixture.OwnershipGuard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_LargeValidId_ReturnsInvariantStringsWithoutPrecisionLoss()
    {
        const ulong jobId = 9_007_199_254_740_993;
        const ulong attemptId = 9_007_199_254_740_992;
        var fixture = CreateFixture(UserRole.Student);
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            fixture.Actor.UserId!.Value,
            jobId,
            attemptId,
            AIJobStatus.Completed);
        fixture.OwnershipGuard
            .Setup(guard => guard.CheckStudentAccessAsync(
                fixture.Actor.UserId.Value,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var result = await fixture.UseCase.ExecuteAsync(
            "9007199254740993",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("9007199254740993", result.Data!.AnalysisJobId);
        Assert.Equal("9007199254740992", result.Data.AttemptId);
        Assert.Equal(
            "/api/v1/learning/attempts/9007199254740992/feedback",
            result.Data.FeedbackUrl);
    }

    [Fact]
    public async Task ExecuteAsync_MaxUlongId_IsValidAndReachesTenantQuery()
    {
        var fixture = CreateFixture(UserRole.CenterManager);

        var result = await fixture.UseCase.ExecuteAsync(
            "18446744073709551615",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        fixture.OwnershipGuard.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(InvalidActorContexts))]
    public async Task ExecuteAsync_InvalidTenantOrRole_FailsClosedWithoutOwnershipCheck(
        TestActorContext actor)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(store, databaseName, actor);
        var ownership = new Mock<IStudentOwnershipGuard>(MockBehavior.Strict);
        var useCase = new GetAnalysisJobStatusUseCase(
            context,
            actor,
            ownership.Object);

        var result = await useCase.ExecuteAsync("13001", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        ownership.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.CenterManager)]
    public async Task ExecuteAsync_CrossTenantJob_ReturnsNotFoundBeforeOwnershipCheck(
        UserRole role)
    {
        var fixture = CreateFixture(role);
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            Guid.NewGuid(),
            13001,
            12001,
            AIJobStatus.Pending);
        var otherCenterId = Guid.NewGuid();
        await SeedJobAsync(
            fixture.Context,
            otherCenterId,
            Guid.NewGuid(),
            13002,
            12002,
            AIJobStatus.Processing);

        var result = await fixture.UseCase.ExecuteAsync(
            "13002",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        fixture.OwnershipGuard.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(OwnershipDecision.Allowed, null)]
    [InlineData(OwnershipDecision.Forbidden, ErrorCodes.ForbiddenResource)]
    [InlineData(OwnershipDecision.NotFound, ErrorCodes.ResourceNotFound)]
    [InlineData((OwnershipDecision)999, ErrorCodes.ResourceNotFound)]
    public async Task ExecuteAsync_OwnershipDecision_MapsFailClosed(
        OwnershipDecision decision,
        string? expectedErrorCode)
    {
        var fixture = CreateFixture(UserRole.Student);
        var studentId = fixture.Actor.UserId!.Value;
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            studentId,
            13001,
            12001,
            AIJobStatus.Processing);
        fixture.OwnershipGuard
            .Setup(guard => guard.CheckStudentAccessAsync(
                studentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);

        var result = await fixture.UseCase.ExecuteAsync(
            "13001",
            CancellationToken.None);

        Assert.Equal(expectedErrorCode is null, result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    [Theory]
    [InlineData(AIJobStatus.Pending, false, null)]
    [InlineData(AIJobStatus.Processing, false, null)]
    [InlineData(
        AIJobStatus.Completed,
        true,
        "/api/v1/learning/attempts/12001/feedback")]
    [InlineData(
        AIJobStatus.FallbackCompleted,
        true,
        "/api/v1/learning/attempts/12001/feedback")]
    [InlineData(AIJobStatus.FailedTerminal, true, null)]
    public async Task ExecuteAsync_EachPersistedStatus_MapsExactContract(
        AIJobStatus status,
        bool expectedTerminal,
        string? expectedFeedbackUrl)
    {
        var fixture = CreateFixture(UserRole.CenterManager);
        var studentId = Guid.NewGuid();
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            studentId,
            13001,
            12001,
            status,
            retryCount: 1);
        fixture.OwnershipGuard
            .Setup(guard => guard.CheckStudentAccessAsync(
                studentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var result = await fixture.UseCase.ExecuteAsync(
            "13001",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        var data = Assert.IsType<AnalysisJobStatusDataDto>(result.Data);
        Assert.Equal("13001", data.AnalysisJobId);
        Assert.Equal("12001", data.AttemptId);
        Assert.Equal(status.ToString(), data.Status);
        Assert.Equal(1, data.RetryCount);
        Assert.Equal(expectedTerminal, data.Terminal);
        Assert.Equal(expectedFeedbackUrl, data.FeedbackUrl);
        Assert.Equal(JobUpdatedAt, data.UpdatedAt);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownPersistedStatus_ThrowsFailClosed()
    {
        var fixture = CreateFixture(UserRole.CenterManager);
        var studentId = Guid.NewGuid();
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            studentId,
            13001,
            12001,
            (AIJobStatus)999);
        fixture.OwnershipGuard
            .Setup(guard => guard.CheckStudentAccessAsync(
                studentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.UseCase.ExecuteAsync("13001", CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_PassesExactCancellationTokenToOwnershipGuard()
    {
        var fixture = CreateFixture(UserRole.Student);
        var studentId = fixture.Actor.UserId!.Value;
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            studentId,
            13001,
            12001,
            AIJobStatus.Pending);
        using var cancellation = new CancellationTokenSource();
        fixture.OwnershipGuard
            .Setup(guard => guard.CheckStudentAccessAsync(
                studentId,
                cancellation.Token))
            .ReturnsAsync(OwnershipDecision.Allowed);

        await fixture.UseCase.ExecuteAsync("13001", cancellation.Token);

        fixture.OwnershipGuard.Verify(guard => guard.CheckStudentAccessAsync(
            studentId,
            cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_CancelsDatabaseQuery()
    {
        var fixture = CreateFixture(UserRole.Student);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.UseCase.ExecuteAsync("13001", cancellation.Token));

        fixture.OwnershipGuard.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_ReadOnlyQuery_DoesNotTrackMutateOrSave()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var actor = ValidActor(UserRole.CenterManager);
        await using (var seedContext = CreateContext(store, databaseName, actor))
        {
            await SeedJobAsync(
                seedContext,
                actor.CenterId!.Value,
                Guid.NewGuid(),
                13001,
                12001,
                AIJobStatus.Processing);
        }

        var saveCounter = new SaveCounterInterceptor();
        await using var queryContext = CreateContext(
            store,
            databaseName,
            actor,
            saveCounter);
        var ownership = new Mock<IStudentOwnershipGuard>();
        ownership
            .Setup(guard => guard.CheckStudentAccessAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);
        var useCase = new GetAnalysisJobStatusUseCase(
            queryContext,
            actor,
            ownership.Object);

        var result = await useCase.ExecuteAsync("13001", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(queryContext.ChangeTracker.Entries());
        Assert.Equal(0, saveCounter.SaveCallCount);
        await using var reloadContext = CreateContext(store, databaseName, actor);
        var job = await reloadContext.AIAnalysisJobs.AsNoTracking().SingleAsync();
        var attempt = await reloadContext.Attempts.AsNoTracking().SingleAsync();
        Assert.Equal(AIJobStatus.Processing, job.Status);
        Assert.Equal(JobUpdatedAt, job.UpdatedAt);
        Assert.Equal(AttemptStatus.PendingAnalysis, attempt.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_StudentOwnership_AllowsOnlyOwnAttempt(
        bool ownsAttempt)
    {
        var fixture = CreateFixture(UserRole.Student, useRealOwnershipGuard: true);
        var studentId = ownsAttempt
            ? fixture.Actor.UserId!.Value
            : Guid.NewGuid();
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            studentId,
            13001,
            12001,
            AIJobStatus.Pending);

        var result = await fixture.UseCase.ExecuteAsync(
            "13001",
            CancellationToken.None);

        Assert.Equal(ownsAttempt, result.IsSuccess);
        Assert.Equal(
            ownsAttempt ? null : ErrorCodes.ForbiddenResource,
            result.ErrorCode);
    }

    [Theory]
    [InlineData(true, ClassStatus.Active, ClassStudentStatus.Active, true)]
    [InlineData(false, ClassStatus.Active, ClassStudentStatus.Active, false)]
    [InlineData(true, ClassStatus.Active, ClassStudentStatus.Removed, false)]
    [InlineData(true, ClassStatus.Archived, ClassStudentStatus.Active, false)]
    public async Task ExecuteAsync_TeacherOwnership_RequiresActiveOwnClassMembership(
        bool teacherOwnsClass,
        ClassStatus classStatus,
        ClassStudentStatus membershipStatus,
        bool expectedAllowed)
    {
        var fixture = CreateFixture(UserRole.Teacher, useRealOwnershipGuard: true);
        var studentId = Guid.NewGuid();
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            studentId,
            13001,
            12001,
            AIJobStatus.Pending,
            classTeacherId: teacherOwnsClass
                ? fixture.Actor.UserId
                : Guid.NewGuid(),
            classStatus: classStatus,
            membershipStatus: membershipStatus);

        var result = await fixture.UseCase.ExecuteAsync(
            "13001",
            CancellationToken.None);

        Assert.Equal(expectedAllowed, result.IsSuccess);
        Assert.Equal(
            expectedAllowed ? null : ErrorCodes.ForbiddenResource,
            result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherWithoutStudentMembership_IsForbidden()
    {
        var fixture = CreateFixture(UserRole.Teacher, useRealOwnershipGuard: true);
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            Guid.NewGuid(),
            13001,
            12001,
            AIJobStatus.Pending);

        var result = await fixture.UseCase.ExecuteAsync(
            "13001",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_CanViewAnyStudentJobInOwnCenter()
    {
        var fixture = CreateFixture(
            UserRole.CenterManager,
            useRealOwnershipGuard: true);
        await SeedJobAsync(
            fixture.Context,
            fixture.Actor.CenterId!.Value,
            Guid.NewGuid(),
            13001,
            12001,
            AIJobStatus.Pending);

        var result = await fixture.UseCase.ExecuteAsync(
            "13001",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    public static TheoryData<TestActorContext> InvalidActorContexts =>
        new()
        {
            new TestActorContext(null, null, null, false),
            new TestActorContext(Guid.Empty, Guid.NewGuid(), nameof(UserRole.Student), true),
            new TestActorContext(Guid.NewGuid(), Guid.Empty, nameof(UserRole.Student), true),
            new TestActorContext(Guid.NewGuid(), Guid.NewGuid(), " ", true),
            new TestActorContext(Guid.NewGuid(), Guid.NewGuid(), "Administrator", true)
        };

    private static Fixture CreateFixture(
        UserRole role,
        bool useRealOwnershipGuard = false)
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var actor = ValidActor(role);
        var context = CreateContext(store, databaseName, actor);
        var ownership = new Mock<IStudentOwnershipGuard>();
        IStudentOwnershipGuard ownershipGuard = useRealOwnershipGuard
            ? new OrganizationOwnershipGuard(context, actor)
            : ownership.Object;
        return new Fixture(
            actor,
            context,
            ownership,
            new GetAnalysisJobStatusUseCase(context, actor, ownershipGuard));
    }

    private static TestActorContext ValidActor(UserRole role) =>
        new(Guid.NewGuid(), Guid.NewGuid(), role.ToString(), true);

    private static EduTwinDbContext CreateContext(
        InMemoryDatabaseRoot store,
        string databaseName,
        TestActorContext actor,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName, store);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new EduTwinDbContext(options.Options, actor);
    }

    private static async Task SeedJobAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid studentId,
        ulong jobId,
        ulong attemptId,
        AIJobStatus status,
        byte retryCount = 0,
        Guid? classTeacherId = null,
        ClassStatus classStatus = ClassStatus.Active,
        ClassStudentStatus membershipStatus = ClassStudentStatus.Active)
    {
        context.Students.Add(new Student
        {
            StudentId = studentId,
            CenterId = centerId,
            FullName = "Polling student",
            GradeLevel = 12,
            CreatedAt = JobUpdatedAt.AddDays(-1),
            UpdatedAt = JobUpdatedAt.AddDays(-1)
        });
        context.Attempts.Add(new Attempt
        {
            AttemptId = attemptId,
            CenterId = centerId,
            StudentId = studentId,
            QuestionId = 9001,
            FinalAnswer = "must-not-be-projected",
            ReasoningText = "must-not-be-projected",
            TimeSpentSeconds = 10,
            Confidence = 50,
            ReasoningLanguage = "en",
            Status = AttemptStatus.PendingAnalysis,
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = JobUpdatedAt.AddMinutes(-2),
            UpdatedAt = JobUpdatedAt.AddMinutes(-2)
        });
        context.AIAnalysisJobs.Add(new AIAnalysisJob
        {
            AnalysisJobId = jobId,
            CenterId = centerId,
            AttemptId = attemptId,
            Status = status,
            RetryCount = retryCount,
            AvailableAt = JobUpdatedAt.AddMinutes(-2),
            CorrelationId = "must-not-be-projected",
            LeaseOwner = status == AIJobStatus.Processing
                ? "must-not-be-projected"
                : null,
            LeaseUntil = status == AIJobStatus.Processing
                ? JobUpdatedAt.AddMinutes(5)
                : null,
            LastErrorCode = "must-not-be-projected",
            LastErrorMessage = "must-not-be-projected",
            CreatedAt = JobUpdatedAt.AddMinutes(-2),
            UpdatedAt = JobUpdatedAt
        });

        if (classTeacherId.HasValue)
        {
            var classId = Guid.NewGuid();
            context.Classes.Add(new Class
            {
                ClassId = classId,
                CenterId = centerId,
                TeacherId = classTeacherId.Value,
                SubjectId = Guid.NewGuid(),
                ClassName = "Polling class",
                AcademicYear = "2026-2027",
                Status = classStatus,
                CreatedAt = JobUpdatedAt.AddDays(-1),
                UpdatedAt = JobUpdatedAt.AddDays(-1)
            });
            context.ClassStudents.Add(new ClassStudent
            {
                CenterId = centerId,
                ClassId = classId,
                StudentId = studentId,
                Status = membershipStatus,
                JoinedAt = JobUpdatedAt.AddDays(-1),
                RemovedAt = membershipStatus == ClassStudentStatus.Removed
                    ? JobUpdatedAt
                    : null
            });
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private sealed record Fixture(
        TestActorContext Actor,
        EduTwinDbContext Context,
        Mock<IStudentOwnershipGuard> OwnershipGuard,
        GetAnalysisJobStatusUseCase UseCase);

    public sealed class TestActorContext(
        Guid? centerId,
        Guid? userId,
        string? role,
        bool isResolved) : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; } = centerId;
        public Guid? UserId { get; } = userId;
        public string? Role { get; } = role;
        public uint? AuthVersion { get; } = 1;
        public bool IsResolved { get; } = isResolved;
    }

    private sealed class SaveCounterInterceptor : SaveChangesInterceptor
    {
        public int SaveCallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
