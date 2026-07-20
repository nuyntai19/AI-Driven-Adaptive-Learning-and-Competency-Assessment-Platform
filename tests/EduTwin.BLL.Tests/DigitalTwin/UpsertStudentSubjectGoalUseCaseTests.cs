using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Organization;
using EduTwin.BLL.DigitalTwin;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.DigitalTwin;

public class TestEduTwinDbContext : EduTwinDbContext
{
    public Func<CancellationToken, Task<int>>? SaveChangesAsyncOverride { get; set; }

    public TestEduTwinDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantIdAccessor)
        : base(options, tenantIdAccessor)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (SaveChangesAsyncOverride != null)
            return SaveChangesAsyncOverride(cancellationToken);
        return base.SaveChangesAsync(cancellationToken);
    }
}

public class UpsertStudentSubjectGoalUseCaseTests : IDisposable
{
    private readonly TestEduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ITenantIdAccessor> _mockTenantIdAccessor;
    private readonly Mock<IStudentOwnershipGuard> _mockOwnershipGuard;
    private readonly Mock<IGoalIdGenerator> _mockGoalIdGenerator;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly UpsertStudentSubjectGoalUseCase _useCase;
    private readonly CancellationTokenSource _cts = new();

    public UpsertStudentSubjectGoalUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _mockTenantIdAccessor = new Mock<ITenantIdAccessor>();
        _dbContext = new TestEduTwinDbContext(options, _mockTenantIdAccessor.Object);

        _mockTenantContext = new Mock<ITenantContext>();
        _mockOwnershipGuard = new Mock<IStudentOwnershipGuard>();
        _mockGoalIdGenerator = new Mock<IGoalIdGenerator>();

        var fixedTime = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(fixedTime);

        _useCase = new UpsertStudentSubjectGoalUseCase(
            _mockTenantContext.Object,
            _mockOwnershipGuard.Object,
            _dbContext,
            _mockGoalIdGenerator.Object,
            _mockTimeProvider.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _cts.Dispose();
    }

    private UpsertStudentSubjectGoalRequest CreateValidRequest(string? rowVersion = null)
    {
        return new UpsertStudentSubjectGoalRequest
        {
            TargetScore = 8.5m,
            RemainingDays = 120,
            RowVersion = rowVersion
        };
    }

    private void SetupTenantContext(Guid? centerId, Guid? userId, string? role, bool isResolved = true)
    {
        _mockTenantContext.Setup(c => c.IsResolved).Returns(isResolved);
        _mockTenantContext.Setup(c => c.CenterId).Returns(centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(userId);
        _mockTenantContext.Setup(c => c.Role).Returns(role);

        if (centerId.HasValue)
        {
            _mockTenantIdAccessor.Setup(a => a.CenterId).Returns(centerId.Value);
        }
    }

    private async Task SeedFixtureAsync(Guid centerId, Guid studentId, Guid subjectId, Guid userId, UserRole role = UserRole.Student, bool active = true)
    {
        var utcNow = DateTime.UtcNow;
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode = "C", Timezone = "UTC", Status = active ? CenterStatus.Active : CenterStatus.Suspended, IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow });
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = centerId, FullName = "S", IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow, User = new EduTwin.DAL.IdentityAndTenancy.User { UserId = userId, CenterId = centerId, RoleName = role, Status = active ? UserStatus.Active : UserStatus.Disabled, IsDeleted = !active, Username = "u", PasswordHash = "h", DisplayName = "d", CreatedAt = utcNow, UpdatedAt = utcNow } });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = active, IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow });
        await _dbContext.SaveChangesAsync();
    }

    private async Task AssertNoGoalCreated()
    {
        var count = await _dbContext.StudentSubjectGoals.IgnoreQueryFilters().CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Create_GeneratorAlwaysReturnsZero_StopsAfterMaxAttempts()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(0);

        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ExecuteAsync(studentId, subjectId, request, _cts.Token));
        Assert.Contains("Failed to generate a valid unique GoalId", ex.Message);

        _mockGoalIdGenerator.Verify(g => g.GenerateId(), Times.Exactly(3));
        await AssertNoGoalCreated();
    }

    [Fact]
    public async Task Create_GeneratorReturnsZeroThenValid_Success()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        _mockGoalIdGenerator.SetupSequence(g => g.GenerateId()).Returns(0).Returns(500ul);

        var request = CreateValidRequest();
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("500", result.Data!.GoalId);
        _mockGoalIdGenerator.Verify(g => g.GenerateId(), Times.Exactly(2));
    }

    [Fact]
    public async Task Create_CancellationPropagates()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        _mockGoalIdGenerator.SetupSequence(g => g.GenerateId()).Returns(0).Returns(100ul);

        _cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token));
        await AssertNoGoalCreated();
    }

    [Theory]
    [InlineData("for key 'student_subject_goals.PRIMARY'")]
    [InlineData("Duplicate entry '100' for key 'PRIMARY' student_subject_goals")]
    [InlineData("pk_student_subject_goals")]
    [InlineData("PK_StudentSubjectGoals")]
    public async Task Create_PKCollision_RetriesAndSucceeds(string exceptionMessage)
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        _mockGoalIdGenerator.SetupSequence(g => g.GenerateId()).Returns(100ul).Returns(101ul);

        int attempts = 0;
        _dbContext.SaveChangesAsyncOverride = (ct) =>
        {
            attempts++;
            if (attempts == 1)
            {
                var inner = new Exception(exceptionMessage);
                var mid = new Exception("wrapper", inner);
                throw new DbUpdateException("outer", mid);
            }
            _dbContext.SaveChangesAsyncOverride = null;
            return _dbContext.SaveChangesAsync(ct);
        };

        var result = await _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("101", result.Data!.GoalId);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Create_CompositeConstraintDeep_ReturnsConflict()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(100ul);

        _dbContext.SaveChangesAsyncOverride = (ct) =>
        {
            var inner = new Exception("ux_student_subject_goals_center_id_student_id_subject_id violated");
            throw new DbUpdateException("outer", new Exception("mid", inner));
        };

        var result = await _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task Create_UnrelatedPrimaryMessage_Rethrows()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(100ul);

        _dbContext.SaveChangesAsyncOverride = (ct) => throw new DbUpdateException("Duplicate entry '1' for key 'PRIMARY' unrelated_table");

        await Assert.ThrowsAsync<DbUpdateException>(() => _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token));
    }

    [Fact]
    public async Task Create_ExistingGoalIdInDifferentTenant_CollisionHandledByDb()
    {
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        var studentA = Guid.NewGuid();
        var subjectA = Guid.NewGuid();

        _mockTenantIdAccessor.Setup(a => a.CenterId).Returns(centerB);
        _dbContext.Centers.Add(new Center { CenterId = centerB, CenterName = "B", CenterCode = "B", Timezone = "UTC", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal { GoalId = 100ul, CenterId = centerB, StudentId = studentA, SubjectId = subjectA, TargetScore = 5m, RemainingDays = 10, CurrentPredictedScore = 0m, RiskScore = 0m, CreatedAt = DateTime.UtcNow, CreatedBy = Guid.Empty, UpdatedAt = DateTime.UtcNow, UpdatedBy = Guid.Empty, IsDeleted = false, RowVersion = 1 });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        SetupTenantContext(centerA, studentA, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentA, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerA, studentA, subjectA, studentA);

        var anyGoal = await _dbContext.StudentSubjectGoals.AnyAsync(g => g.GoalId == 100ul);
        Assert.False(anyGoal);

        _mockGoalIdGenerator.SetupSequence(g => g.GenerateId()).Returns(100ul).Returns(101ul);

        int attempts = 0;
        _dbContext.SaveChangesAsyncOverride = (ct) =>
        {
            attempts++;
            if (attempts == 1)
                throw new DbUpdateException("Duplicate key for key 'student_subject_goals.PRIMARY'");

            _dbContext.SaveChangesAsyncOverride = null;
            return _dbContext.SaveChangesAsync(ct);
        };

        var result = await _useCase.ExecuteAsync(studentA, subjectA, CreateValidRequest(), _cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("101", result.Data!.GoalId);
        Assert.Equal(2, attempts);

        var goalB = await _dbContext.StudentSubjectGoals.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.GoalId == 100ul);
        Assert.NotNull(goalB);
        Assert.Equal(centerB, goalB.CenterId);
    }

    [Fact]
    public async Task Create_StudentSelfCreate_Success()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(111ul);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        var result = await _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token);
        Assert.True(result.IsSuccess);
        Assert.Equal("111", result.Data!.GoalId);
    }

    [Fact]
    public async Task Create_TeacherOwnership_Success()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "Teacher");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(222ul);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        var result = await _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token);
        Assert.Null(result.ErrorCode);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Create_CenterManager_Success()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(333ul);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        var result = await _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token);
        Assert.Null(result.ErrorCode);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("EmptyCenter", "Student")]
    [InlineData("EmptyUser", "Student")]
    [InlineData("NullCenter", "Student")]
    [InlineData("NullUser", "Student")]
    [InlineData("MissingRole", null)]
    [InlineData("EmptyRole", " ")]
    [InlineData("WrongCaseRole", "student")]
    [InlineData("NumericRole", "1")]
    public async Task Context_Invalid_ReturnsNotFound(string scenario, string? roleName)
    {
        Guid? centerId = Guid.NewGuid();
        Guid? userId = Guid.NewGuid();
        if (scenario == "EmptyCenter") centerId = Guid.Empty;
        if (scenario == "EmptyUser") userId = Guid.Empty;
        if (scenario == "NullCenter") centerId = null;
        if (scenario == "NullUser") userId = null;

        SetupTenantContext(centerId, userId, roleName);
        var result = await _useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CreateValidRequest(), _cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Center_Suspended_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");

        var utcNow = DateTime.UtcNow;
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode = "C", Timezone = "UTC", Status = CenterStatus.Suspended, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(studentId, Guid.NewGuid(), CreateValidRequest(), _cts.Token);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(UserStatus.Disabled, false, true)]
    [InlineData(UserStatus.Active, true, true)]
    [InlineData(UserStatus.Active, false, false)]
    public async Task StudentOrSubject_Invalid_ReturnsNotFound(UserStatus studentStatus, bool studentDeleted, bool subjectActive)
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);

        var utcNow = DateTime.UtcNow;
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode = "C", Timezone = "UTC", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = centerId, FullName = "S", IsDeleted = studentDeleted, CreatedAt = utcNow, UpdatedAt = utcNow, User = new EduTwin.DAL.IdentityAndTenancy.User { UserId = studentId, CenterId = centerId, RoleName = UserRole.Student, Status = studentStatus, IsDeleted = false, Username = "u", PasswordHash = "h", DisplayName = "d", CreatedAt = utcNow, UpdatedAt = utcNow } });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = subjectActive, IsDeleted = !subjectActive, CreatedAt = utcNow, UpdatedAt = utcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Subject_CrossTenant_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);

        var utcNow = DateTime.UtcNow;
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode = "C", Timezone = "UTC", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, CenterName = "O", CenterCode = "O", Timezone = "UTC", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = centerId, FullName = "S", IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow, User = new EduTwin.DAL.IdentityAndTenancy.User { UserId = studentId, CenterId = centerId, RoleName = UserRole.Student, Status = UserStatus.Active, IsDeleted = false, Username = "u", PasswordHash = "h", DisplayName = "d", CreatedAt = utcNow, UpdatedAt = utcNow } });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = otherCenterId, SubjectCode = "S", SubjectName = "S", IsActive = true, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(studentId, subjectId, CreateValidRequest(), _cts.Token);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task OwnershipGuard_ExactCancellationToken_Passed()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Forbidden);
        await SeedFixtureAsync(centerId, studentId, Guid.NewGuid(), studentId);

        var result = await _useCase.ExecuteAsync(studentId, Guid.NewGuid(), CreateValidRequest(), _cts.Token);

        _mockOwnershipGuard.Verify(g => g.CheckStudentAccessAsync(studentId, _cts.Token), Times.Once);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task Create_WithNonNullRowVersion_ValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        var request = CreateValidRequest("1");
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cts.Token);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Update_MissingRowVersion_ValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        _dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal { GoalId = 999ul, CenterId = centerId, StudentId = studentId, SubjectId = subjectId, TargetScore = 5m, RemainingDays = 10, CurrentPredictedScore = 2m, RiskScore = 0m, CreatedAt = DateTime.UtcNow, CreatedBy = studentId, UpdatedAt = DateTime.UtcNow, UpdatedBy = studentId, IsDeleted = false, RowVersion = 1 });
        await _dbContext.SaveChangesAsync();

        var request = CreateValidRequest(null);
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cts.Token);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Update_WithStaleRowVersion_ReturnsConflict()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        _dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal { GoalId = 999ul, CenterId = centerId, StudentId = studentId, SubjectId = subjectId, TargetScore = 5m, RemainingDays = 10, CurrentPredictedScore = 2m, RiskScore = 0m, CreatedAt = DateTime.UtcNow, CreatedBy = studentId, UpdatedAt = DateTime.UtcNow, UpdatedBy = studentId, IsDeleted = false, RowVersion = 5 });
        await _dbContext.SaveChangesAsync();

        var request = CreateValidRequest("4");
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cts.Token);

        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task Update_KeepsImmutableFields_IncrementsRowVersion()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var goalId = 999ul;

        SetupTenantContext(centerId, studentId, "Student");
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cts.Token)).ReturnsAsync(OwnershipDecision.Allowed);
        await SeedFixtureAsync(centerId, studentId, subjectId, studentId);

        var originalTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        _dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal { GoalId = goalId, CenterId = centerId, StudentId = studentId, SubjectId = subjectId, TargetScore = 5m, RemainingDays = 10, CurrentPredictedScore = 2m, RiskScore = 0m, CreatedAt = originalTime, CreatedBy = Guid.Empty, UpdatedAt = originalTime, UpdatedBy = Guid.Empty, IsDeleted = false, RowVersion = 5 });
        await _dbContext.SaveChangesAsync();

        var request = CreateValidRequest("1");
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cts.Token);

        Assert.Null(result.ErrorCode);
        Assert.True(result.IsSuccess);
        var updatedGoal = await _dbContext.StudentSubjectGoals.FirstAsync(g => g.GoalId == goalId);

        Assert.Equal(originalTime, updatedGoal.CreatedAt);
        Assert.Equal(Guid.Empty, updatedGoal.CreatedBy);
        Assert.Equal(_mockTimeProvider.Object.GetUtcNow().UtcDateTime, updatedGoal.UpdatedAt);
        Assert.Equal(studentId, updatedGoal.UpdatedBy);
        Assert.Equal(2m, updatedGoal.CurrentPredictedScore);
        Assert.Equal(2ul, updatedGoal.RowVersion);
    }
}
