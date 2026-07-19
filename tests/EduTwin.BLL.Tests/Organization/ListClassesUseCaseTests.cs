using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class ListClassesUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<ListClassesUseCase>> _mockLogger;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public ListClassesUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_userId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));
        _mockLogger = new Mock<ILogger<ListClassesUseCase>>();
    }

    private EduTwinDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns((Guid?)_centerId);
        var context = new EduTwinDbContext(options, mockAccessor.Object);
        return context;
    }

    private async Task SeedDataAsync(
        EduTwinDbContext context,
        Guid classId,
        Guid? teacherId = null,
        Guid? subjectId = null,
        ClassStatus classStatus = ClassStatus.Active,
        bool isDeleted = false,
        Guid? centerId = null,
        bool isCrossTenant = false)
    {
        var cid = centerId ?? _centerId;
        var tId = teacherId ?? Guid.NewGuid();
        var sId = subjectId ?? Guid.NewGuid();

        var center = await context.Centers.FindAsync(cid);
        if (center == null)
        {
            context.Centers.Add(new Center
            {
                CenterId = cid,
                CenterName = "Test Center",
                CenterCode = "TC-" + cid.ToString().Substring(0, 4),
                Timezone = "UTC",
                Status = CenterStatus.Active,
                RowVersion = 1,
                CreatedAt = _fixedTime,
                UpdatedAt = _fixedTime
            });
        }

        var teacherUser = new User
        {
            UserId = tId,
            CenterId = cid,
            Username = "teacher_" + tId,
            PasswordHash = "hash",
            DisplayName = "Teacher Name",
            RoleName = UserRole.Teacher,
            Status = UserStatus.Active,
            RowVersion = 1,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        };
        var teacher = new Teacher
        {
            TeacherId = tId,
            CenterId = cid,
            User = teacherUser,
            RowVersion = 1,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        };

        var subject = new Subject
        {
            SubjectId = sId,
            CenterId = cid,
            SubjectName = "Math",
            SubjectCode = "MATH",
            RowVersion = 1,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        };

        var cls = new Class
        {
            ClassId = classId,
            CenterId = isCrossTenant ? Guid.NewGuid() : cid,
            TeacherId = tId,
            SubjectId = sId,
            ClassName = "Class A",
            AcademicYear = "2026-2027",
            Status = classStatus,
            IsDeleted = isDeleted,
            RowVersion = 1,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            Teacher = teacher,
            Subject = subject
        };

        context.Users.Add(teacherUser);
        context.Teachers.Add(teacher);
        context.Subjects.Add(subject);
        context.Classes.Add(cls);

        await context.SaveChangesAsync();
    }

    private async Task SeedClassStudentAsync(EduTwinDbContext context, Guid classId, ClassStudentStatus status = ClassStudentStatus.Active)
    {
        var studentId = Guid.NewGuid();
        var studentUser = new User
        {
            UserId = studentId,
            CenterId = _centerId,
            Username = "student_" + studentId,
            PasswordHash = "hash",
            DisplayName = "Student Name",
            RoleName = UserRole.Student,
            Status = UserStatus.Active,
            RowVersion = 1,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        };
        var student = new Student
        {
            StudentId = studentId,
            CenterId = _centerId,
            User = studentUser,
            FullName = "Student Name",
            GradeLevel = 10,
            RowVersion = 1,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        };

        var classStudent = new ClassStudent
        {
            ClassId = classId,
            StudentId = studentId,
            Status = status,
            JoinedAt = _fixedTime,
            CenterId = _centerId
        };

        context.Users.Add(studentUser);
        context.Students.Add(student);
        context.ClassStudents.Add(classStudent);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task T01_CenterManager_ListsClasses_Success()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        await SeedDataAsync(context, c1);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.ToString(), result.Data[0].ClassId);
    }

    [Fact]
    public async Task T02_Teacher_OnlySeesOwnClasses()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(context, c1, teacherId: _userId); // Own class
        await SeedDataAsync(context, c2); // Other teacher's class

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.ToString(), result.Data[0].ClassId);
    }

    [Fact]
    public async Task T03_CenterManager_FiltersByTeacherId()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var t1 = Guid.NewGuid();

        await SeedDataAsync(context, c1, teacherId: t1);
        await SeedDataAsync(context, c2);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { TeacherId = t1 });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.ToString(), result.Data[0].ClassId);
    }

    [Fact]
    public async Task T04_CrossTenantClasses_Excluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(context, c1);
        await SeedDataAsync(context, c2, centerId: Guid.NewGuid());

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.ToString(), result.Data[0].ClassId);
    }

    [Fact]
    public async Task T05_SoftDeletedClasses_Excluded()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(context, c1);
        await SeedDataAsync(context, c2, isDeleted: true);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(c1.ToString(), result.Data[0].ClassId);
    }

    [Fact]
    public async Task T06_ActiveStudentCount_IsAccurate()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        await SeedDataAsync(context, c1);

        await SeedClassStudentAsync(context, c1, ClassStudentStatus.Active);
        await SeedClassStudentAsync(context, c1, ClassStudentStatus.Active);
        await SeedClassStudentAsync(context, c1, ClassStudentStatus.Removed); // Inactive

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data![0].StudentCount);
    }

    [Fact]
    public async Task T07_UndefinedStatus_ValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedDataAsync(context, Guid.NewGuid());

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { Status = (ClassStatus)999 });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task T08_EmptyGuidFilters_ValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedDataAsync(context, Guid.NewGuid());

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { TeacherId = Guid.Empty });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("teacher")]
    [InlineData("centermanager")]
    [InlineData("Admin")]
    [InlineData("  ")]
    [InlineData(null)]
    public async Task T09_InvalidRole_ReturnsResourceNotFound(string? role)
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedDataAsync(context, Guid.NewGuid());

        _mockTenantContext.Setup(t => t.Role).Returns(role);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T10_PaginationAndOrdering()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);

        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(context, c1);
        await SeedDataAsync(context, c2);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { Page = 1, PageSize = 1 });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task CanceledToken_CancelsExecution()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedDataAsync(context, Guid.NewGuid());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await sut.ExecuteAsync(new ClassListQuery(), cts.Token));
    }
    [Fact]
    public async Task CenterManager_SubjectIdFilter()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(context, c1, subjectId: s1);
        await SeedDataAsync(context, c2, subjectId: s2);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { SubjectId = s1 });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(c1.ToString("D").ToLowerInvariant(), result.Data![0].ClassId);
    }

    [Fact]
    public async Task CenterManager_StatusFilter()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        await SeedDataAsync(context, c1, classStatus: ClassStatus.Active);
        await SeedDataAsync(context, c2, classStatus: ClassStatus.Archived);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { Status = ClassStatus.Archived });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal(c2.ToString("D").ToLowerInvariant(), result.Data![0].ClassId);
    }

    [Fact]
    public async Task SubjectIdEmpty_ValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { SubjectId = Guid.Empty });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Teacher_QueryTeacherIdCannotExpandOwnership()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid(); // own class
        var c2 = Guid.NewGuid(); // other teacher's class
        var t2 = Guid.NewGuid(); // other teacher

        await SeedDataAsync(context, c1, teacherId: _userId);
        await SeedDataAsync(context, c2, teacherId: t2);

        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        // Teacher tries to query another teacher's classes
        var result = await sut.ExecuteAsync(new ClassListQuery { TeacherId = t2 });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!); // Should be empty because Teacher scope restricts to _userId
    }

    [Fact]
    public async Task EmptyResult_TotalPagesIsZero()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedDataAsync(context, Guid.NewGuid()); // Seed to ensure center exists

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery { SubjectId = Guid.NewGuid(), Page = 1, PageSize = 10 });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Projection_MapsSubjectTeacherStatusAndCanonicalIds()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        var t1 = Guid.NewGuid();
        var s1 = Guid.NewGuid();

        await SeedDataAsync(context, c1, teacherId: t1, subjectId: s1, classStatus: ClassStatus.Active);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!);

        Assert.Equal(c1.ToString("D").ToLowerInvariant(), item.ClassId);
        Assert.Equal(s1.ToString("D").ToLowerInvariant(), item.Subject.SubjectId);
        Assert.Equal("Math", item.Subject.SubjectName);
        Assert.Equal(t1.ToString("D").ToLowerInvariant(), item.Teacher.TeacherId);
        Assert.Equal("Teacher Name", item.Teacher.DisplayName);
        Assert.Equal("Active", item.Status);
    }

    [Fact]
    public async Task Projection_RowVersionUsesInvariantCulture()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();

        await SeedDataAsync(context, c1);

        var originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");

            var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(new ClassListQuery());

            Assert.True(result.IsSuccess);
            Assert.Equal("1", result.Data![0].RowVersion);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task ReadQuery_DoesNotTrackEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        await SeedDataAsync(context, c1);
        context.ChangeTracker.Clear();

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ActiveStudentCount_ExcludesInactiveMembership()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var c1 = Guid.NewGuid();
        await SeedDataAsync(context, c1);

        await SeedClassStudentAsync(context, c1, ClassStudentStatus.Active);
        await SeedClassStudentAsync(context, c1, ClassStudentStatus.Removed);

        var sut = new ListClassesUseCase(context, _mockTenantContext.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(new ClassListQuery());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data![0].StudentCount);
    }
}
