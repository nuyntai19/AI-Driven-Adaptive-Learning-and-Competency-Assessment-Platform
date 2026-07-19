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

        var classId = Guid.NewGuid();
        dbContext.Classes.Add(new Class
        {
            ClassId = classId,
            CenterId = _centerId,
            ClassName = "Class 1",
            AcademicYear = "2023",
            TeacherId = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Status = ClassStatus.Active,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            RowVersion = 1
        });

        dbContext.ClassStudents.Add(new ClassStudent
        {
            ClassId = classId,
            StudentId = studentId,
            CenterId = _centerId,
            Status = ClassStudentStatus.Removed,
            JoinedAt = _fixedTime
        });
        await dbContext.SaveChangesAsync();

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

        var classId = Guid.NewGuid();
        dbContext.Classes.Add(new Class
        {
            ClassId = classId,
            CenterId = _centerId,
            ClassName = "Class 1",
            AcademicYear = "2023",
            TeacherId = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Status = ClassStatus.Archived,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            RowVersion = 1
        });

        dbContext.ClassStudents.Add(new ClassStudent
        {
            ClassId = classId,
            StudentId = studentId,
            CenterId = _centerId,
            Status = ClassStudentStatus.Active,
            JoinedAt = _fixedTime
        });
        await dbContext.SaveChangesAsync();

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
        dbContext.Classes.Add(new Class
        {
            ClassId = ownedClassId,
            CenterId = _centerId,
            ClassName = "Owned",
            AcademicYear = "2023",
            TeacherId = _userId, // Owned by this teacher
            SubjectId = Guid.NewGuid(),
            Status = ClassStatus.Active,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            RowVersion = 1
        });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = ownedClassId, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });

        var otherClassId = Guid.NewGuid();
        dbContext.Classes.Add(new Class
        {
            ClassId = otherClassId,
            CenterId = _centerId,
            ClassName = "Other",
            AcademicYear = "2023",
            TeacherId = Guid.NewGuid(), // Other teacher
            SubjectId = Guid.NewGuid(),
            Status = ClassStatus.Active,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            RowVersion = 1
        });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = otherClassId, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });

        await dbContext.SaveChangesAsync();

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

        var c1 = Guid.NewGuid();
        dbContext.Classes.Add(new Class
        {
            ClassId = c1, CenterId = _centerId, ClassName = "C1", AcademicYear = "2023", TeacherId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Status = ClassStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1
        });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = c1, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });

        var c2 = Guid.NewGuid();
        dbContext.Classes.Add(new Class
        {
            ClassId = c2, CenterId = _centerId, ClassName = "C2", AcademicYear = "2023", TeacherId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Status = ClassStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1
        });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = c2, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });

        await dbContext.SaveChangesAsync();

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

        var c1 = Guid.NewGuid();
        dbContext.Classes.Add(new Class
        {
            ClassId = c1, CenterId = _centerId, ClassName = "C1", AcademicYear = "2023", TeacherId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Status = ClassStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1
        });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = c1, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });
        await dbContext.SaveChangesAsync();

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
        dbContext.Classes.Add(new Class { ClassId = ownedClassId, CenterId = _centerId, ClassName = "Owned", AcademicYear = "2023", TeacherId = _userId, SubjectId = Guid.NewGuid(), Status = ClassStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1 });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = ownedClassId, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });

        var otherClassId = Guid.NewGuid();
        dbContext.Classes.Add(new Class { ClassId = otherClassId, CenterId = _centerId, ClassName = "Other", AcademicYear = "2023", TeacherId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Status = ClassStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1 });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = otherClassId, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });

        await dbContext.SaveChangesAsync();

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

        var classId = Guid.NewGuid();
        dbContext.Classes.Add(new Class { ClassId = classId, CenterId = _centerId, ClassName = "C1", AcademicYear = "2023", TeacherId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Status = ClassStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1 });
        
        // Active student
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = classId, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });
        
        // Removed student
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = classId, StudentId = Guid.NewGuid(), CenterId = _centerId, Status = ClassStudentStatus.Removed, JoinedAt = _fixedTime });

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

        dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1,
            CenterId = _centerId,
            StudentId = studentId,
            SubjectId = Guid.NewGuid(),
            TargetScore = 80,
            RemainingDays = 10,
            CurrentPredictedScore = 75,
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
        var dbContext = CreateContext(dbName);
        var studentId = Guid.NewGuid();
        await SeedDataAsync(dbContext, studentId);

        // Soft deleted
        dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1, CenterId = _centerId, StudentId = studentId, SubjectId = Guid.NewGuid(), IsDeleted = true, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1
        });

        // Other student
        dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 2, CenterId = _centerId, StudentId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1
        });

        await dbContext.SaveChangesAsync();

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

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
        dbContext.Classes.Add(new Class { ClassId = classId, CenterId = _centerId, ClassName = "C1", AcademicYear = "2023", TeacherId = teacherId, SubjectId = subjectId, Status = ClassStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime, RowVersion = 1 });
        dbContext.ClassStudents.Add(new ClassStudent { ClassId = classId, StudentId = studentId, CenterId = _centerId, Status = ClassStudentStatus.Active, JoinedAt = _fixedTime });
        
        await dbContext.SaveChangesAsync();

        var sut = new GetStudentUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(studentId);

        Assert.True(result.IsSuccess);
        var c = result.Data!.Classes[0];
        Assert.Equal(classId.ToString("D"), c.ClassId);
        Assert.Equal(subjectId.ToString("D"), c.Subject.SubjectId);
        Assert.Equal(teacherId.ToString("D"), c.Teacher.TeacherId);
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
