using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.DigitalTwin;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.DAL.Organization;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.DigitalTwin;

public class ListStudentSubjectGoalsUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenant;
    private readonly Mock<IStudentOwnershipGuard> _mockGuard;
    private readonly ListStudentSubjectGoalsUseCase _useCase;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly CultureInfo _originalCulture;

    private readonly string _databaseName;

    public ListStudentSubjectGoalsUseCaseTests()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _databaseName = Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenant = new Mock<ITenantContext>();
        _mockTenant.Setup(t => t.IsResolved).Returns(true);
        _mockTenant.Setup(t => t.CenterId).Returns(_centerId);
        _mockTenant.Setup(t => t.UserId).Returns(_userId);
        _mockTenant.Setup(t => t.Role).Returns("CenterManager");

        _mockGuard = new Mock<IStudentOwnershipGuard>();
        _mockGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(OwnershipDecision.Allowed);

        _useCase = new ListStudentSubjectGoalsUseCase(_mockTenant.Object, _mockGuard.Object, _dbContext);

        SeedCenter(_centerId, CenterStatus.Active, false);
    }

    private EduTwinDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(tenantId);
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    private void SeedCenter(Guid centerId, CenterStatus status, bool isDeleted)
    {
        _dbContext.Centers.Add(new Center { CenterId = centerId, Status = status, IsDeleted = isDeleted, CenterName = "Test Center", CenterCode = "TC", Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();
    }

    private async Task SeedFixtureAsync(Guid centerId, Guid studentId, Guid subjectId, Guid userId, UserRole role = UserRole.Student, bool active = true, bool userDeleted = false)
    {
        var utcNow = DateTime.UtcNow;
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = centerId, FullName = "S", GradeLevel = 1, IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow, User = new User { UserId = userId, CenterId = centerId, RoleName = role, Status = active ? UserStatus.Active : UserStatus.Disabled, IsDeleted = userDeleted, Username = "u", DisplayName = "d", PasswordHash = "h", CreatedAt = utcNow, UpdatedAt = utcNow } });
        if (subjectId != Guid.Empty)
        {
            _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = active, IsDeleted = !active, CreatedAt = utcNow, UpdatedAt = utcNow });
        }
        await _dbContext.SaveChangesAsync();
    }

    private StudentSubjectGoal SeedGoal(ulong goalId, Guid centerId, Guid studentId, Guid subjectId, bool isDeleted = false)
    {
        var goal = new StudentSubjectGoal { GoalId = goalId, CenterId = centerId, StudentId = studentId, SubjectId = subjectId, IsDeleted = isDeleted, TargetScore = 8.5m, RemainingDays = 30, CurrentPredictedScore = 8.0m, RiskScore = 0.5m, RowVersion = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.Empty, UpdatedBy = Guid.Empty };
        _dbContext.StudentSubjectGoals.Add(goal);
        _dbContext.SaveChanges();
        return goal;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CenterManager_ListGoals_Success()
    {
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);
        SeedGoal(1UL, _centerId, studentId, subjectId);

        _mockTenant.Setup(t => t.Role).Returns("CenterManager");

        var result = await _useCase.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task TeacherWithOwnership_ListGoals_Success()
    {
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);
        SeedGoal(1UL, _centerId, studentId, subjectId);

        _mockTenant.Setup(t => t.Role).Returns("Teacher");

        var result = await _useCase.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task StudentSelf_ListGoals_Success()
    {
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);
        SeedGoal(1UL, _centerId, studentId, subjectId);

        _mockTenant.Setup(t => t.Role).Returns("Student");

        var result = await _useCase.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task NoGoals_ReturnsEmptySuccessCollection()
    {
        var studentId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, Guid.Empty, studentId);

        var result = await _useCase.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task EmptyStudentId_ReturnsNotFoundWithoutGuardOrQuery()
    {
        var result = await _useCase.ExecuteAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockGuard.Verify(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnresolvedTenant_ReturnsNotFound()
    {
        _mockTenant.Setup(t => t.IsResolved).Returns(false);
        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingOrEmptyCenterId_ReturnsNotFound()
    {
        _mockTenant.Setup(t => t.CenterId).Returns((Guid?)null);
        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        _mockTenant.Setup(t => t.CenterId).Returns(Guid.Empty);
        result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingOrEmptyUserId_ReturnsNotFound()
    {
        _mockTenant.Setup(t => t.UserId).Returns((Guid?)null);
        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        _mockTenant.Setup(t => t.UserId).Returns(Guid.Empty);
        result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Admin")]
    [InlineData("student")]
    [InlineData("TEACHER")]
    [InlineData("0")]
    [InlineData("1")]
    public async Task InvalidRole_ReturnsNotFound(string? role)
    {
        _mockTenant.Setup(t => t.Role).Returns(role);
        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingDeletedOrSuspendedCenter_ReturnsNotFound()
    {
        var otherCenter = Guid.NewGuid();
        _mockTenant.Setup(t => t.CenterId).Returns(otherCenter);
        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        SeedCenter(otherCenter, CenterStatus.Active, true);
        result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var suspendedCenter = Guid.NewGuid();
        _mockTenant.Setup(t => t.CenterId).Returns(suspendedCenter);
        SeedCenter(suspendedCenter, CenterStatus.Suspended, false);
        result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task OwnershipForbidden_ReturnsForbidden()
    {
        _mockGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(OwnershipDecision.Forbidden);

        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task OwnershipNotFound_ReturnsNotFound()
    {
        _mockGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(OwnershipDecision.NotFound);

        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UndefinedOwnershipDecision_ReturnsNotFound()
    {
        _mockGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((OwnershipDecision)999);

        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingStudent_ReturnsNotFound()
    {
        var result = await _useCase.ExecuteAsync(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeletedStudent_ReturnsNotFound()
    {
        var studentId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, Guid.Empty, studentId, active: false);

        var result = await _useCase.ExecuteAsync(studentId);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantStudent_ReturnsNotFound()
    {
        var otherCenter = Guid.NewGuid();
        _dbContext.Centers.Add(new Center { CenterId = otherCenter, Status = CenterStatus.Active, IsDeleted = false, CenterName = "O", CenterCode = "O", Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var studentId = Guid.NewGuid();

        // Temporarily bypass query filters so we can seed cross-tenant data.
        var utcNow = DateTime.UtcNow;
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = otherCenter, FullName = "S", GradeLevel = 1, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow, User = new User { UserId = studentId, CenterId = otherCenter, RoleName = UserRole.Student, Status = UserStatus.Active, IsDeleted = false, Username = "u", DisplayName = "d", PasswordHash = "h", CreatedAt = utcNow, UpdatedAt = utcNow } });
        await _dbContext.SaveChangesAsync();

        await using (var dbContextB = CreateContext(otherCenter))
        {
            var studentB = await dbContextB.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
            Assert.NotNull(studentB);
        }

        var result = await _useCase.ExecuteAsync(studentId);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CurrentTenantStudent_WithCrossTenantUser_ReturnsNotFound()
    {
        var otherCenter = Guid.NewGuid();
        _dbContext.Centers.Add(new Center { CenterId = otherCenter, Status = CenterStatus.Active, IsDeleted = false, CenterName = "O", CenterCode = "O", Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var studentId = Guid.NewGuid();

        var utcNow = DateTime.UtcNow;
        _dbContext.Students.Add(new Student { StudentId = studentId, CenterId = _centerId, FullName = "S", GradeLevel = 1, IsDeleted = false, CreatedAt = utcNow, UpdatedAt = utcNow });
        _dbContext.Users.Add(new User { UserId = studentId, CenterId = otherCenter, RoleName = UserRole.Student, Status = UserStatus.Active, IsDeleted = false, Username = "u", DisplayName = "d", PasswordHash = "h", CreatedAt = utcNow, UpdatedAt = utcNow });
        await _dbContext.SaveChangesAsync();

        var studentA = await _dbContext.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
        Assert.NotNull(studentA);

        await using (var dbContextB = CreateContext(otherCenter))
        {
            var userB = await dbContextB.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == studentId);
            Assert.NotNull(userB);
        }

        var result = await _useCase.ExecuteAsync(studentId);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task DeletedUserOrWrongRole_ReturnsNotFound()
    {
        var studentId1 = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId1, Guid.Empty, studentId1, userDeleted: true);

        var result1 = await _useCase.ExecuteAsync(studentId1);
        Assert.Equal(ErrorCodes.ResourceNotFound, result1.ErrorCode);

        var studentId2 = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId2, Guid.Empty, studentId2, role: UserRole.Teacher);

        var result2 = await _useCase.ExecuteAsync(studentId2);
        Assert.Equal(ErrorCodes.ResourceNotFound, result2.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedGoals_AreExcluded()
    {
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);
        SeedGoal(1UL, _centerId, studentId, subjectId, isDeleted: true);
        SeedGoal(2UL, _centerId, studentId, subjectId, isDeleted: false);

        var result = await _useCase.ExecuteAsync(studentId);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("2", result.Data![0].GoalId);
    }

    [Fact]
    public async Task CrossTenantGoals_AreExcluded()
    {
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);

        var otherCenterId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, Status = CenterStatus.Active, IsDeleted = false, CenterName = "O", CenterCode = "O", Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var otherSubjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject { SubjectId = otherSubjectId, CenterId = otherCenterId, SubjectCode = "S", SubjectName = "S", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        SeedGoal(1UL, _centerId, studentId, subjectId, isDeleted: false);

        var goal = new StudentSubjectGoal { GoalId = 2UL, CenterId = otherCenterId, StudentId = studentId, SubjectId = otherSubjectId, IsDeleted = false, TargetScore = 8.5m, RemainingDays = 30, CurrentPredictedScore = 8.0m, RiskScore = 0.5m, RowVersion = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.Empty, UpdatedBy = Guid.Empty };
        _dbContext.StudentSubjectGoals.Add(goal);
        await _dbContext.SaveChangesAsync();

        await using (var dbContextB = CreateContext(otherCenterId))
        {
            var subjectB = await dbContextB.Subjects.FirstOrDefaultAsync(s => s.SubjectId == otherSubjectId);
            Assert.NotNull(subjectB);

            var goalB = await dbContextB.StudentSubjectGoals.FirstOrDefaultAsync(g => g.GoalId == 2UL);
            Assert.NotNull(goalB);
        }

        var result = await _useCase.ExecuteAsync(studentId);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("1", result.Data![0].GoalId);
    }

    [Fact]
    public async Task CrossTenantOrDeletedSubjectGoals_AreExcluded()
    {
        var studentId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, Guid.Empty, studentId);

        var otherCenterId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, Status = CenterStatus.Active, IsDeleted = false, CenterName = "O", CenterCode = "O", Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var otherSubjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject { SubjectId = otherSubjectId, CenterId = otherCenterId, SubjectCode = "S", SubjectName = "S", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        var deletedSubjectId = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject { SubjectId = deletedSubjectId, CenterId = _centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        SeedGoal(1UL, _centerId, studentId, otherSubjectId);
        SeedGoal(2UL, _centerId, studentId, deletedSubjectId);

        await using (var dbContextB = CreateContext(otherCenterId))
        {
            var subjectB = await dbContextB.Subjects.FirstOrDefaultAsync(s => s.SubjectId == otherSubjectId);
            Assert.NotNull(subjectB);
        }

        // Use IgnoreQueryFilters just in test to prove the malformed cross-tenant goal (Center A with Subject B) was saved
        var goalA1 = await _dbContext.StudentSubjectGoals.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.GoalId == 1UL);
        Assert.NotNull(goalA1);

        var result = await _useCase.ExecuteAsync(studentId);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Projection_ReturnsEveryExactField()
    {
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);
        SeedGoal(1234567890UL, _centerId, studentId, subjectId);

        var result = await _useCase.ExecuteAsync(studentId);
        var dto = result.Data!.First();

        Assert.Equal("1234567890", dto.GoalId);
        Assert.Equal(studentId.ToString("D"), dto.StudentId);
        Assert.Equal(subjectId.ToString("D"), dto.SubjectId);
        Assert.Equal(8.5m, dto.TargetScore);
        Assert.Equal(30, dto.RemainingDays);
        Assert.Equal(8.0m, dto.CurrentPredictedScore);
        Assert.Equal(0.5m, dto.RiskScore);
        Assert.Equal("1", dto.RowVersion);
    }

    [Fact]
    public async Task Ordering_IsSubjectIdThenGoalId()
    {
        var studentId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, Guid.Empty, studentId);

        var subjectId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var subjectId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId1, CenterId = _centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId2, CenterId = _centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        SeedGoal(2UL, _centerId, studentId, subjectId1);
        SeedGoal(1UL, _centerId, studentId, subjectId1);
        SeedGoal(3UL, _centerId, studentId, subjectId2);

        var result = await _useCase.ExecuteAsync(studentId);
        var data = result.Data!;

        Assert.Equal(3, data.Count);
        Assert.Equal("1", data[0].GoalId);
        Assert.Equal("2", data[1].GoalId);
        Assert.Equal("3", data[2].GoalId);
    }

    [Fact]
    public async Task NumericFormatting_IsInvariantUnderNonInvariantCulture()
    {
        CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // uses comma for decimal separator

        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);
        SeedGoal(1000UL, _centerId, studentId, subjectId);

        var result = await _useCase.ExecuteAsync(studentId);
        var dto = result.Data!.First();

        Assert.Equal("1000", dto.GoalId);
        Assert.Equal("1", dto.RowVersion);
    }

    [Fact]
    public async Task Read_DoesNotTrackEntities()
    {
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, subjectId, studentId);
        SeedGoal(1UL, _centerId, studentId, subjectId);

        _dbContext.ChangeTracker.Clear();

        await _useCase.ExecuteAsync(studentId);

        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExactCancellationTokenPassedToOwnershipGuard()
    {
        var cts = new CancellationTokenSource();
        var studentId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, Guid.Empty, studentId);

        await _useCase.ExecuteAsync(studentId, cts.Token);

        _mockGuard.Verify(g => g.CheckStudentAccessAsync(studentId, cts.Token), Times.Once);
    }

    [Fact]
    public async Task Cancellation_IsPropagated()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var studentId = Guid.NewGuid();
        await SeedFixtureAsync(_centerId, studentId, Guid.Empty, studentId);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _useCase.ExecuteAsync(studentId, cts.Token));
    }
}
