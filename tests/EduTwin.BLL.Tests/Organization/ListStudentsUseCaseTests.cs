using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class ListStudentsUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IClassOwnershipGuard> _mockOwnershipGuard;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    public ListStudentsUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_managerId);
        _mockTenantContext.Setup(c => c.Role).Returns("CenterManager");

        _mockOwnershipGuard = new Mock<IClassOwnershipGuard>();
    }

    private EduTwinDbContext CreateContext(string dbName, Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(tenantId ?? _centerId);
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    private async Task SeedDataAsync(
        EduTwinDbContext context,
        Guid studentId,
        Guid? centerId = null,
        bool centerIsDeleted = false,
        CenterStatus centerStatus = CenterStatus.Active,
        bool isDeleted = false,
        bool userIsDeleted = false,
        UserRole userRole = UserRole.Student,
        UserStatus userStatus = UserStatus.Active,
        Guid? classId = null,
        bool classIsDeleted = false,
        ClassStatus classStatus = ClassStatus.Active,
        Guid? teacherId = null,
        ClassStudentStatus membershipStatus = ClassStudentStatus.Active)
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
                IsDeleted = centerIsDeleted,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            });
        }

        context.Users.Add(new User
        {
            UserId = studentId,
            CenterId = targetCenterId,
            Username = "student_" + studentId.ToString().Substring(0, 4),
            DisplayName = "Full Name " + studentId.ToString().Substring(0, 4),
            PasswordHash = "hash",
            RoleName = userRole,
            Status = userStatus,
            AuthVersion = 1,
            IsDeleted = userIsDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        context.Students.Add(new Student
        {
            StudentId = studentId,
            CenterId = targetCenterId,
            FullName = "Full Name " + studentId.ToString().Substring(0, 4),
            GradeLevel = 10,
            IsDeleted = isDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        if (classId.HasValue)
        {
            if (!await context.Classes.AnyAsync(c => c.ClassId == classId.Value))
            {
                context.Classes.Add(new Class
                {
                    ClassId = classId.Value,
                    CenterId = targetCenterId,
                    TeacherId = teacherId ?? Guid.NewGuid(),
                    ClassName = "Test Class",
                    AcademicYear = "2024",
                    Status = classStatus,
                    IsDeleted = classIsDeleted,
                    CreatedAt = _fixedTime.UtcDateTime,
                    UpdatedAt = _fixedTime.UtcDateTime
                });
            }

            context.ClassStudents.Add(new ClassStudent
            {
                ClassId = classId.Value,
                StudentId = studentId,
                CenterId = targetCenterId,
                Status = membershipStatus,
                JoinedAt = _fixedTime.UtcDateTime
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public void DependencyInjection_ResolvesListStudentsUseCase()
    {
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("DI").Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        services.AddSingleton(new EduTwinDbContext(options, mockAccessor.Object));
        services.AddSingleton(_mockTenantContext.Object);
        services.AddSingleton(_mockOwnershipGuard.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddOrganization();
        var provider = services.BuildServiceProvider();
        var useCase = provider.GetRequiredService<IListStudentsUseCase>();
        Assert.NotNull(useCase);
    }

    [Fact]
    public async Task CenterManager_ListStudents_ReturnsAllTenantStudents()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);
        await SeedDataAsync(dbContext, s2, classId: c1);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(2, result.TotalItems);
    }

    [Fact]
    public async Task Teacher_ListStudents_ReturnsOnlyOwnedActiveClassStudents()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);

        var teacherId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.UserId).Returns(teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns("Teacher");

        var s1 = Guid.NewGuid(); // My student
        var s2 = Guid.NewGuid(); // Other teacher's student
        var s3 = Guid.NewGuid(); // No class
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(dbContext, s1, classId: c1, teacherId: teacherId);
        await SeedDataAsync(dbContext, s2, classId: c2, teacherId: Guid.NewGuid());
        await SeedDataAsync(dbContext, s3);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(s1, result.Data![0].StudentId);
    }

    [Fact]
    public async Task Teacher_DoesNotSeeStudentInAnotherTeachersClass()
    {
        // Covered implicitly by the previous test, but we can have an explicit one.
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);

        var teacherId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.UserId).Returns(teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns("Teacher");

        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, classId: Guid.NewGuid(), teacherId: Guid.NewGuid()); // other teacher

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Teacher_DoesNotSeeRemovedMembership()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);

        var teacherId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.UserId).Returns(teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns("Teacher");

        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, classId: Guid.NewGuid(), teacherId: teacherId, membershipStatus: ClassStudentStatus.Removed);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Teacher_DoesNotSeeStudentOnlyInArchivedClass()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);

        var teacherId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.UserId).Returns(teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns("Teacher");

        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, classId: Guid.NewGuid(), teacherId: teacherId, classStatus: ClassStatus.Archived);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task ClassIdFilter_ReturnsOnlyActiveMembers()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var c1 = Guid.NewGuid();

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(c1, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, classId: c1); // active

        var s2 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s2, classId: c1, membershipStatus: ClassStudentStatus.Removed); // removed

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { ClassId = c1 });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(s1, result.Data![0].StudentId);
    }

    [Fact]
    public async Task CenterManager_ClassIdFilter_Works()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var c1 = Guid.NewGuid();

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(c1, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, classId: c1);
        var s2 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s2); // no class

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { ClassId = c1 });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(s1, result.Data![0].StudentId);
    }

    [Fact]
    public async Task Teacher_OwnedClassId_Allowed()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.UserId).Returns(teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns("Teacher");

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(c1, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, classId: c1, teacherId: teacherId);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { ClassId = c1 });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(s1, result.Data![0].StudentId);
    }

    [Fact]
    public async Task Teacher_OtherTeachersClassId_Forbidden()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, Guid.NewGuid());
        var teacherId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.UserId).Returns(teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns("Teacher");

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(c1, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Forbidden);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { ClassId = c1 });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantClassId_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, Guid.NewGuid());

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(c1, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.NotFound);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { ClassId = c1 });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UndefinedOwnershipDecision_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, Guid.NewGuid());

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(c1, It.IsAny<CancellationToken>())).ReturnsAsync((OwnershipDecision)99);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { ClassId = c1 });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantStudent_IsExcluded()
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
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();

        var otherCenterId = Guid.NewGuid();
        var otherDbContext = CreateContext(dbName, otherCenterId);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(otherDbContext, s1, centerId: otherCenterId);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task SoftDeletedStudent_IsExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, isDeleted: true);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task SoftDeletedUser_IsExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, userIsDeleted: true);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task UserWrongRole_IsExcluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, userRole: UserRole.Teacher);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Search_ByUsername()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);

        var student = await dbContext.Users.FirstAsync();

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Search = student.Username.Substring(1, 4) });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Search_ByFullName()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);

        var student = await dbContext.Students.FirstAsync();

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Search = student.FullName.Substring(1, 4) });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Search_Whitespace_TreatedAsNoFilter()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Search = "   " });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task RawSearchOver200_ValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        await SeedDataAsync(dbContext, Guid.NewGuid());

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Search = new string('a', 201) });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UndefinedStatus_ValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        await SeedDataAsync(dbContext, Guid.NewGuid());

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Status = (UserStatus)99 });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public async Task GradeLevel_10_11_12_Accepted(byte grade)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);
        var student = await dbContext.Students.FirstAsync();
        student.GradeLevel = grade;
        await dbContext.SaveChangesAsync();

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { GradeLevel = grade });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    public async Task GradeLevelBelow10OrAbove12_ValidationFailed(byte grade)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        await SeedDataAsync(dbContext, Guid.NewGuid());

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { GradeLevel = grade });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task PageAndPageSizeBoundaryValidation(int page, int pageSize)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        await SeedDataAsync(dbContext, Guid.NewGuid());

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Page = page, PageSize = pageSize });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task PageBeyondLast_ReturnsCorrectTotalMetadata()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Page = 2, PageSize = 10 });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task Ordering_IsFullNameThenStudentId()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);

        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var s3 = Guid.NewGuid();

        await SeedDataAsync(dbContext, s1);
        await SeedDataAsync(dbContext, s2);
        await SeedDataAsync(dbContext, s3);

        var student1 = await dbContext.Students.FirstAsync(s => s.StudentId == s1);
        var student2 = await dbContext.Students.FirstAsync(s => s.StudentId == s2);
        var student3 = await dbContext.Students.FirstAsync(s => s.StudentId == s3);

        student1.FullName = "B";
        student2.FullName = "C";
        student3.FullName = "A";

        await dbContext.SaveChangesAsync();

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Count);
        Assert.Equal("A", result.Data![0].FullName);
        Assert.Equal("B", result.Data![1].FullName);
        Assert.Equal("C", result.Data![2].FullName);
    }

    [Fact]
    public async Task ActiveClassCount_CenterManagerCountsAllVisibleActiveMemberships()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(dbContext, s1, classId: c1); // active

        dbContext.Classes.Add(new Class
        {
            ClassId = c2,
            CenterId = _centerId,
            TeacherId = Guid.NewGuid(),
            ClassName = "Test Class 2",
            AcademicYear = "2024",
            Status = ClassStatus.Active,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        dbContext.ClassStudents.Add(new ClassStudent
        {
            ClassId = c2,
            StudentId = s1,
            CenterId = _centerId,
            Status = ClassStudentStatus.Active,
            JoinedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(2, result.Data![0].ActiveClassCount);
    }

    [Fact]
    public async Task ActiveClassCount_TeacherCountsOnlyOwnedClasses()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        _mockTenantContext.Setup(c => c.UserId).Returns(teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns("Teacher");

        // Owned class
        await SeedDataAsync(dbContext, s1, classId: c1, teacherId: teacherId);

        // Not owned class
        dbContext.Classes.Add(new Class
        {
            ClassId = c2,
            CenterId = _centerId,
            TeacherId = Guid.NewGuid(),
            ClassName = "Test Class 2",
            AcademicYear = "2024",
            Status = ClassStatus.Active,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        dbContext.ClassStudents.Add(new ClassStudent
        {
            ClassId = c2,
            StudentId = s1,
            CenterId = _centerId,
            Status = ClassStudentStatus.Active,
            JoinedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(1, result.Data![0].ActiveClassCount);
    }

    [Fact]
    public async Task Query_DoesNotTrackEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);
        dbContext.ChangeTracker.Clear();

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(false, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")]
    [InlineData(true, null, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", null, "CenterManager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "00000000-0000-0000-0000-000000000000", "CenterManager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Admin")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Student")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "centermanager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", " ")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", null)]
    public async Task FailClosedCases_ResourceNotFound(bool isResolved, string? centerIdStr, string? userIdStr, string? role)
    {
        Guid? centerId = centerIdStr == null ? null : Guid.Parse(centerIdStr);
        Guid? userId = userIdStr == null ? null : Guid.Parse(userIdStr);

        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(c => c.IsResolved).Returns(isResolved);
        if (centerId.HasValue) mockTenant.Setup(c => c.CenterId).Returns(centerId.Value);
        else mockTenant.Setup(c => c.CenterId).Returns((Guid?)null);
        if (userId.HasValue) mockTenant.Setup(c => c.UserId).Returns(userId.Value);
        else mockTenant.Setup(c => c.UserId).Returns((Guid?)null);
        mockTenant.Setup(c => c.Role).Returns(role);

        var sut = new ListStudentsUseCase(dbContext, mockTenant.Object, _mockOwnershipGuard.Object);

        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, CenterStatus.Active)]
    [InlineData(false, CenterStatus.Suspended)]
    public async Task CenterDeletedOrSuspended_ResourceNotFound(bool centerIsDeleted, CenterStatus centerStatus)
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1, centerIsDeleted: centerIsDeleted, centerStatus: centerStatus);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task PageIntMax_ReturnsEmptyCollectionWithoutOverflow()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);

        var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(new StudentListQuery { Page = int.MaxValue, PageSize = 100 });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task Projection_ReturnsInvariantRowVersion()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        await SeedDataAsync(dbContext, s1);

        // Ensure current culture is not invariant to test formatting
        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");

            var sut = new ListStudentsUseCase(dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
            var result = await sut.ExecuteAsync(new StudentListQuery());

            Assert.True(result.IsSuccess);
            Assert.Single(result.Data!);
            var rowVersion = result.Data![0].RowVersion;
            Assert.NotNull(rowVersion);
            Assert.Equal("1", rowVersion);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}
