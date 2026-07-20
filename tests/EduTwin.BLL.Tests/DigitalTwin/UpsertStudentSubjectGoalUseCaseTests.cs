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

public class UpsertStudentSubjectGoalUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ITenantIdAccessor> _mockTenantIdAccessor;
    private readonly Mock<IStudentOwnershipGuard> _mockOwnershipGuard;
    private readonly Mock<IGoalIdGenerator> _mockGoalIdGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly UpsertStudentSubjectGoalUseCase _useCase;
    private readonly CancellationToken _cancellationToken = CancellationToken.None;

    public UpsertStudentSubjectGoalUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _mockTenantIdAccessor = new Mock<ITenantIdAccessor>();
        _dbContext = new EduTwinDbContext(options, _mockTenantIdAccessor.Object);

        _mockTenantContext = new Mock<ITenantContext>();
        _mockOwnershipGuard = new Mock<IStudentOwnershipGuard>();
        _mockGoalIdGenerator = new Mock<IGoalIdGenerator>();
        _timeProvider = TimeProvider.System;

        _useCase = new UpsertStudentSubjectGoalUseCase(
            _mockTenantContext.Object,
            _mockOwnershipGuard.Object,
            _dbContext,
            _mockGoalIdGenerator.Object,
            _timeProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
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

    private void SetupTenantContext(Guid centerId, Guid userId, UserRole role)
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns(centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(userId);
        _mockTenantContext.Setup(c => c.Role).Returns(role.ToString());
        
        _mockTenantIdAccessor.Setup(a => a.CenterId).Returns(centerId);
    }

    private async Task SeedFixtureAsync(Guid centerId, Guid studentId, Guid subjectId, Guid userId, UserRole role = UserRole.Student, bool active = true)
    {
        var utcNow = DateTime.UtcNow;
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode = "C", Timezone = "UTC", Status = active ? CenterStatus.Active : CenterStatus.Suspended, IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow });
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = centerId, FullName = "S", IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow, User = new EduTwin.DAL.IdentityAndTenancy.User { UserId = userId, CenterId = centerId, RoleName = UserRole.Student, Status = active ? UserStatus.Active : UserStatus.Disabled, IsDeleted = !active, Username = "u", PasswordHash = "h", DisplayName = "d", CreatedAt = utcNow, UpdatedAt = utcNow } });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = active, IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Upsert_StudentSelfCreate_Success()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var userId = studentId;
        var goalId = 999ul;

        SetupTenantContext(centerId, userId, UserRole.Student);
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cancellationToken)).ReturnsAsync(OwnershipDecision.Allowed);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(goalId);

        await SeedFixtureAsync(centerId, studentId, subjectId, userId);

        var request = CreateValidRequest();
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cancellationToken);

        Assert.True(result.IsSuccess, "Error: " + result.ErrorCode);
        Assert.NotNull(result.Data);
        Assert.Equal(goalId.ToString(), result.Data.GoalId);
        Assert.Equal("1", result.Data.RowVersion);
        Assert.Equal(0m, result.Data.CurrentPredictedScore);
        Assert.Equal(68.00m, result.Data.RiskScore);
    }

    [Fact]
    public async Task Upsert_TeacherOwnerCreate_Success()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var teacherUserId = Guid.NewGuid();
        var studentUserId = studentId;
        var goalId = 999ul;

        SetupTenantContext(centerId, teacherUserId, UserRole.Teacher);
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cancellationToken)).ReturnsAsync(OwnershipDecision.Allowed);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(goalId);

        await SeedFixtureAsync(centerId, studentId, subjectId, studentUserId);

        var request = CreateValidRequest();
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Upsert_CenterManagerCreate_Success()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        var studentUserId = studentId;
        var goalId = 999ul;

        SetupTenantContext(centerId, managerUserId, UserRole.CenterManager);
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cancellationToken)).ReturnsAsync(OwnershipDecision.Allowed);
        _mockGoalIdGenerator.Setup(g => g.GenerateId()).Returns(goalId);

        await SeedFixtureAsync(centerId, studentId, subjectId, studentUserId);

        var request = CreateValidRequest();
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Upsert_ForbiddenDecision_ReturnsForbidden()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var studentUserId = studentId;

        SetupTenantContext(centerId, userId, UserRole.Teacher);
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cancellationToken)).ReturnsAsync(OwnershipDecision.Forbidden);

        await SeedFixtureAsync(centerId, studentId, subjectId, studentUserId);

        var request = CreateValidRequest();
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task Upsert_CrossTenantStudent_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var userId = studentId;

        SetupTenantContext(centerId, userId, UserRole.Student);
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cancellationToken)).ReturnsAsync(OwnershipDecision.Allowed);

        var otherCenterId = Guid.NewGuid();
        // Seed with different centerId for student
        var utcNow = DateTime.UtcNow;
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode = "C", Timezone = "UTC", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = otherCenterId, FullName = "S", IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow, User = new EduTwin.DAL.IdentityAndTenancy.User { UserId = userId, CenterId = otherCenterId, RoleName = UserRole.Student, Status = UserStatus.Active, IsDeleted = false, Username = "u", PasswordHash = "h", DisplayName = "d", CreatedAt = utcNow, UpdatedAt = utcNow } });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        await _dbContext.SaveChangesAsync();

        var request = CreateValidRequest();
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Upsert_Update_KeepsPredictedScore_RecalculatesRisk_IncrementsRowVersion()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var userId = studentId;
        var goalId = 999ul;

        SetupTenantContext(centerId, userId, UserRole.Student);
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cancellationToken)).ReturnsAsync(OwnershipDecision.Allowed);

        await SeedFixtureAsync(centerId, studentId, subjectId, userId);

        var utcNow = DateTime.UtcNow;
        _dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = goalId, CenterId = centerId, StudentId = studentId, SubjectId = subjectId,
            TargetScore = 5m, RemainingDays = 10, CurrentPredictedScore = 2m, RiskScore = 0m,
            CreatedAt = utcNow, CreatedBy = userId, UpdatedAt = utcNow, UpdatedBy = userId, IsDeleted = false, RowVersion = 5
        });
        await _dbContext.SaveChangesAsync();

        // RowVersion becomes 1 due to EF interceptor on Added entities!
        var request = CreateValidRequest("1");
        request.TargetScore = 9m;
        request.RemainingDays = 180;

        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cancellationToken);

        Assert.True(result.IsSuccess, "Error: " + result.ErrorCode);
        Assert.Equal("2", result.Data!.RowVersion);
        Assert.Equal(2m, result.Data.CurrentPredictedScore);
        Assert.Equal(49m, result.Data.RiskScore); // Target 9, predicted 2 -> gap 0.7. Time pressure min -> 0.7. Risk = 100 * 0.7 * 0.7 = 49.
    }

    [Fact]
    public async Task Upsert_UpdateWithStaleRowVersion_ReturnsConflict()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var userId = studentId;
        var goalId = 999ul;

        SetupTenantContext(centerId, userId, UserRole.Student);
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, _cancellationToken)).ReturnsAsync(OwnershipDecision.Allowed);

        await SeedFixtureAsync(centerId, studentId, subjectId, userId);

        var utcNow = DateTime.UtcNow;
        _dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = goalId, CenterId = centerId, StudentId = studentId, SubjectId = subjectId,
            TargetScore = 5m, RemainingDays = 10, CurrentPredictedScore = 2m, RiskScore = 0m,
            CreatedAt = utcNow, CreatedBy = userId, UpdatedAt = utcNow, UpdatedBy = userId, IsDeleted = false, RowVersion = 5
        });
        await _dbContext.SaveChangesAsync();

        var request = CreateValidRequest("4");
        var result = await _useCase.ExecuteAsync(studentId, subjectId, request, _cancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }
}
