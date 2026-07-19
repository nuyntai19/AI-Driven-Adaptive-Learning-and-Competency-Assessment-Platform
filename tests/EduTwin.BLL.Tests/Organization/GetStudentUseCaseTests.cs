using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class GetStudentUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IStudentOwnershipGuard> _mockOwnershipGuard;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GetStudentUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_userId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        _mockOwnershipGuard = new Mock<IStudentOwnershipGuard>();
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);
    }

    private EduTwinDbContext CreateContext(string dbName, Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(tenantId ?? _centerId);
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    private async Task SeedDataAsync(
        EduTwinDbContext context,
        Guid studentId,
        Guid? centerId = null,
        Guid? teacherId = null,
        bool isStudentDeleted = false,
        bool isUserDeleted = false,
        UserRole role = UserRole.Student,
        bool isCenterDeleted = false,
        CenterStatus centerStatus = CenterStatus.Active)
    {
        var targetCenterId = centerId ?? _centerId;

        if (!await context.Centers.AnyAsync(c => c.CenterId == targetCenterId))
        {
            context.Centers.Add(new Center
            {
                CenterId = targetCenterId,
                CenterName = "Test Center",
                CenterCode = "TC",
                Timezone = "UTC",
                Status = centerStatus,
                CreatedAt = _fixedTime,
                UpdatedAt = _fixedTime,
                IsDeleted = isCenterDeleted
            });
        }

        if (!await context.Users.AnyAsync(u => u.UserId == studentId))
        {
            context.Users.Add(new User
            {
                UserId = studentId,
                CenterId = targetCenterId,
                Username = $"user_{studentId}",
                DisplayName = "Display Name",
                PasswordHash = "hash",
                RoleName = role,
                Status = UserStatus.Active,
                CreatedAt = _fixedTime,
                UpdatedAt = _fixedTime,
                IsDeleted = isUserDeleted
            });
        }

        if (role == UserRole.Student && !await context.Students.AnyAsync(s => s.StudentId == studentId))
        {
            context.Students.Add(new Student
            {
                StudentId = studentId,
                CenterId = targetCenterId,
                FullName = $"Full Name {studentId.ToString()[..4]}",
                GradeLevel = 10,
                CreatedAt = _fixedTime,
                UpdatedAt = _fixedTime,
                IsDeleted = isStudentDeleted,
                RowVersion = 1
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task<Class> SeedClassAsync(
        EduTwinDbContext context,
        Guid studentId,
        Guid? classId = null,
        Guid? centerId = null,
        Guid? teacherId = null,
        Guid? subjectId = null,
        string className = "C1",
        ClassStatus classStatus = ClassStatus.Active,
        ClassStudentStatus membershipStatus = ClassStudentStatus.Active,
        bool isSubjectDeleted = false,
        bool isTeacherDeleted = false,
        bool isTeacherUserDeleted = false)
    {
        var targetCenterId = centerId ?? _centerId;
        var targetClassId = classId ?? Guid.NewGuid();
        var targetTeacherId = teacherId ?? Guid.NewGuid();
        var targetSubjectId = subjectId ?? Guid.NewGuid();

        if (!await context.Subjects.AnyAsync(s => s.SubjectId == targetSubjectId))
        {
            context.Subjects.Add(new Subject
            {
                SubjectId = targetSubjectId,
                CenterId = targetCenterId,
                SubjectCode = $"SUB_{targetSubjectId.ToString()[..8]}",
                SubjectName = $"Subject {targetSubjectId.ToString()[..4]}",
                IsActive = true,
                CreatedAt = _fixedTime,
                UpdatedAt = _fixedTime,
                IsDeleted = isSubjectDeleted
            });
        }

        if (!await context.Users.AnyAsync(u => u.UserId == targetTeacherId))
        {
            context.Users.Add(new User
            {
                UserId = targetTeacherId,
                CenterId = targetCenterId,
                Username = $"teacher_{targetTeacherId}",
                DisplayName = $"Teacher {targetTeacherId.ToString()[..4]}",
                PasswordHash = "hash",
                RoleName = UserRole.Teacher,
                Status = UserStatus.Active,
                CreatedAt = _fixedTime,
                UpdatedAt = _fixedTime,
                IsDeleted = isTeacherUserDeleted
            });
        }

        if (!await context.Teachers.AnyAsync(t => t.TeacherId == targetTeacherId))
        {
            context.Teachers.Add(new Teacher
            {
                TeacherId = targetTeacherId,
                CenterId = targetCenterId,
                CreatedAt = _fixedTime,
                UpdatedAt = _fixedTime,
                IsDeleted = isTeacherDeleted
            });
        }

        var classEntity = new Class
        {
            ClassId = targetClassId,
            CenterId = targetCenterId,
            ClassName = className,
            AcademicYear = "2023",
            TeacherId = targetTeacherId,
            SubjectId = targetSubjectId,
            Status = classStatus,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            RowVersion = 1
        };
        context.Classes.Add(classEntity);

        if (membershipStatus == ClassStudentStatus.Active)
        {
            context.ClassStudents.Add(new ClassStudent
            {
                ClassId = targetClassId,
                StudentId = studentId,
                CenterId = targetCenterId,
                Status = ClassStudentStatus.Active,
                JoinedAt = _fixedTime
            });
        }
        else if (membershipStatus == ClassStudentStatus.Removed)
        {
            context.ClassStudents.Add(new ClassStudent
            {
                ClassId = targetClassId,
                StudentId = studentId,
                CenterId = targetCenterId,
                Status = ClassStudentStatus.Removed,
                JoinedAt = _fixedTime
            });
        }

        await context.SaveChangesAsync();
        return classEntity;
    }

    [Fact]
    public async Task CenterManager_SameTenant_ReturnsStudentDetail()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(studentId.ToString("D"), result.Data!.StudentId);
    }

    [Fact]
    public async Task Teacher_WithOwnership_ReturnsStudentDetail()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Teacher_WithoutOwnership_Forbidden()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Forbidden);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task Student_OwnId_ReturnsStudentDetail()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = _userId; // Student is requesting own data
        await SeedDataAsync(dbContext, studentId);

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Student));
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Student_OtherId_Forbidden()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Student));
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Forbidden);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantStudent_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        dbContext.Centers.Add(new Center
        {
            CenterId = _centerId,
            CenterName = "Test",
            CenterCode = "TC",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        });
        await dbContext.SaveChangesAsync();

        var otherCenterId = Guid.NewGuid();
        var otherDbContext = CreateContext(dbName, otherCenterId);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(otherDbContext, studentId, centerId: otherCenterId);

        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UndefinedOwnershipDecision_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OwnershipDecision)99);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task EmptyStudentId_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, "d9c6615b-2403-46d2-b66e-c7667d7162b7", "847250e2-887e-40f0-8c28-98e3b5eef927", "CenterManager")]
    [InlineData(true, null, "847250e2-887e-40f0-8c28-98e3b5eef927", "CenterManager")]
    [InlineData(true, "d9c6615b-2403-46d2-b66e-c7667d7162b7", null, "CenterManager")]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "847250e2-887e-40f0-8c28-98e3b5eef927", "CenterManager")]
    [InlineData(true, "d9c6615b-2403-46d2-b66e-c7667d7162b7", "00000000-0000-0000-0000-000000000000", "CenterManager")]
    [InlineData(true, "d9c6615b-2403-46d2-b66e-c7667d7162b7", "847250e2-887e-40f0-8c28-98e3b5eef927", "Admin")]
    [InlineData(true, "d9c6615b-2403-46d2-b66e-c7667d7162b7", "847250e2-887e-40f0-8c28-98e3b5eef927", "")]
    [InlineData(true, "d9c6615b-2403-46d2-b66e-c7667d7162b7", "847250e2-887e-40f0-8c28-98e3b5eef927", null)]
    public async Task MissingOrEmptyContextValues_AndWrongRole_ResourceNotFound(bool isResolved, string? centerIdStr, string? userIdStr, string? role)
    {
        Guid? centerId = centerIdStr == null ? null : Guid.Parse(centerIdStr);
        Guid? userId = userIdStr == null ? null : Guid.Parse(userIdStr);

        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(c => c.IsResolved).Returns(isResolved);
        mockTenant.Setup(c => c.CenterId).Returns(centerId);
        mockTenant.Setup(c => c.UserId).Returns(userId);
        mockTenant.Setup(c => c.Role).Returns(role);

        var sut = new GetStudentUseCase(dbContext, mockTenant.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, CenterStatus.Active)]
    [InlineData(false, CenterStatus.Suspended)]
    public async Task DeletedOrSuspendedCenter_ResourceNotFound(bool isDeleted, CenterStatus status)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId, isCenterDeleted: isDeleted, centerStatus: status);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedStudent_IsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId, isStudentDeleted: true);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedUser_IsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId, isUserDeleted: true);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UserWrongRole_IsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId, role: UserRole.Teacher);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task RemovedMembership_IsExcludedFromClasses()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        await SeedClassAsync(dbContext, studentId, membershipStatus: ClassStudentStatus.Removed);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Classes);
    }

    [Fact]
    public async Task ArchivedOrDeletedClass_IsExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        await SeedClassAsync(dbContext, studentId, classStatus: ClassStatus.Archived);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Classes);
    }

    [Fact]
    public async Task Teacher_ResponseContainsOnlyOwnedClasses()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var ownedClassId = Guid.NewGuid();
        await SeedClassAsync(dbContext, studentId, classId: ownedClassId, teacherId: _userId, className: "Owned");

        var otherClassId = Guid.NewGuid();
        await SeedClassAsync(dbContext, studentId, classId: otherClassId, className: "Other");

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Classes);
        Assert.Equal(ownedClassId.ToString("D"), result.Data.Classes[0].ClassId);
        Assert.Equal(1, result.Data.ActiveClassCount);
    }

    [Fact]
    public async Task CenterManager_ResponseContainsAllVisibleActiveClasses()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        await SeedClassAsync(dbContext, studentId, className: "C1");
        await SeedClassAsync(dbContext, studentId, className: "C2");

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Classes.Count);
        Assert.Equal(2, result.Data.ActiveClassCount);
    }

    [Fact]
    public async Task Student_ResponseContainsOwnVisibleActiveClasses()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = _userId;
        await SeedDataAsync(dbContext, studentId);

        await SeedClassAsync(dbContext, studentId, className: "C1");

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Student));

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Classes);
        Assert.Equal(1, result.Data.ActiveClassCount);
    }

    [Fact]
    public async Task ActiveClassCount_DoesNotLeakOtherTeacherClasses()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var ownedClassId = Guid.NewGuid();
        await SeedClassAsync(dbContext, studentId, classId: ownedClassId, teacherId: _userId, className: "Owned");

        var otherClassId = Guid.NewGuid();
        await SeedClassAsync(dbContext, studentId, classId: otherClassId, className: "Other");

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ActiveClassCount);
    }

    [Fact]
    public async Task ClassStudentCount_CountsOnlyActiveMemberships()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var classEntity = await SeedClassAsync(dbContext, studentId, className: "C1");

        // Removed student
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = classEntity.ClassId, StudentId = Guid.NewGuid(), CenterId = _centerId, Status = ClassStudentStatus.Removed, JoinedAt = _fixedTime });
        await dbContext.SaveChangesAsync();

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Classes[0].StudentCount);
    }

    [Fact]
    public async Task SubjectGoals_SameTenantStudentOnly()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var subjectId = Guid.NewGuid();
        dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = _centerId, SubjectCode = "SA", SubjectName = "SA", IsActive = true, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });

        dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1,
            CenterId = _centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TargetScore = 8.0m,
            RemainingDays = 10,
            CurrentPredictedScore = 7.5m,
            RiskScore = 5,
            RowVersion = 1,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        });

        await dbContext.SaveChangesAsync();

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.SubjectGoals);
        Assert.Equal("1", result.Data.SubjectGoals[0].GoalId);
    }

    [Fact]
    public async Task SoftDeletedAndCrossTenantGoals_AreExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContextA = CreateContext(dbName);
        var studentIdA = Guid.NewGuid();
        await SeedDataAsync(dbContextA, studentIdA);

        var subjectIdA = Guid.NewGuid();
        dbContextA.Subjects.Add(new Subject { SubjectId = subjectIdA, CenterId = _centerId, SubjectCode = "SA2", SubjectName = "SA2", IsActive = true, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });

        // Soft deleted goal for A
        dbContextA.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1, CenterId = _centerId, StudentId = studentIdA, SubjectId = subjectIdA, TargetScore = 8.0m, IsDeleted = true, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1
        });
        await dbContextA.SaveChangesAsync();

        var centerIdB = Guid.NewGuid();
        var dbContextB = CreateContext(dbName, centerIdB);
        // Create Center B, Student B, Subject B
        dbContextB.Centers.Add(new Center { CenterId = centerIdB, CenterName = "Center B", CenterCode = "CB", Timezone = "UTC", Status = CenterStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
        var studentIdB = Guid.NewGuid();
        dbContextB.Users.Add(new User { UserId = studentIdB, CenterId = centerIdB, Username = "sb", PasswordHash = "hash", DisplayName = "sb", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
        dbContextB.Students.Add(new Student { StudentId = studentIdB, CenterId = centerIdB, FullName = "sb", GradeLevel = 10, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1 });
        var subjectIdB = Guid.NewGuid();
        dbContextB.Subjects.Add(new Subject { SubjectId = subjectIdB, CenterId = centerIdB, SubjectCode = "SB", SubjectName = "SB", IsActive = true, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });

        // Active Goal for B
        dbContextB.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 2, CenterId = centerIdB, StudentId = studentIdB, SubjectId = subjectIdB, TargetScore = 9.0m, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1
        });

        await dbContextB.SaveChangesAsync();

        // Assert directly before calling use case
        var bGoalExists = await dbContextB.StudentSubjectGoals.AnyAsync(g => g.GoalId == 2);
        Assert.True(bGoalExists);

        var sut = new GetStudentUseCase(dbContextA, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentIdA);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.SubjectGoals);
    }

    [Fact]
    public async Task DTO_IDs_AreCanonicalStrings()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var classId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        await SeedClassAsync(dbContext, studentId, classId: classId, teacherId: teacherId, subjectId: subjectId, className: "C1");

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        var c = result.Data!.Classes[0];
        Assert.Equal(classId.ToString("D"), c.ClassId);
        Assert.Equal(subjectId.ToString("D"), c.Subject.SubjectId);
        Assert.Equal(teacherId.ToString("D"), c.Teacher.TeacherId);
    }

    [Fact]
    public async Task ClassProjection_ReturnsSeededSubjectAndTeacherNames()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var classId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        await SeedClassAsync(dbContext, studentId, classId: classId, teacherId: teacherId, subjectId: subjectId, className: "C1");

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Classes);
        var c = result.Data.Classes[0];
        Assert.False(string.IsNullOrEmpty(c.Subject.SubjectName));
        Assert.False(string.IsNullOrEmpty(c.Teacher.DisplayName));
        Assert.Equal($"Subject {subjectId.ToString()[..4]}", c.Subject.SubjectName);
        Assert.Equal($"Teacher {teacherId.ToString()[..4]}", c.Teacher.DisplayName);
    }

    [Fact]
    public async Task ClassWithSoftDeletedSubject_IsExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        await SeedClassAsync(dbContext, studentId, isSubjectDeleted: true);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Classes);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ClassWithSoftDeletedTeacherOrUser_IsExcluded(bool isTeacherDeleted, bool isUserDeleted)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        await SeedClassAsync(dbContext, studentId, isTeacherDeleted: isTeacherDeleted, isTeacherUserDeleted: isUserDeleted);

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Classes);
    }

    [Fact]
    public async Task RowVersions_AreInvariantStrings()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");

            var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
            var result = await sut.ExecuteAsync(studentId);

            Assert.True(result.IsSuccess);
            Assert.Equal("1", result.Data!.RowVersion);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Query_DoesNotTrackEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);
        dbContext.ChangeTracker.Clear();

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public void DI_ResolvesGetStudentUseCase()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<EduTwinDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped(sp => _mockTenantContext.Object);
        services.AddScoped(sp => _mockOwnershipGuard.Object);
        services.AddScoped(sp => new Mock<ITenantIdAccessor>().Object);
        services.AddOrganization();

        var provider = services.BuildServiceProvider();
        var sut = provider.GetService<IGetStudentUseCase>();

        Assert.NotNull(sut);
    }
}
