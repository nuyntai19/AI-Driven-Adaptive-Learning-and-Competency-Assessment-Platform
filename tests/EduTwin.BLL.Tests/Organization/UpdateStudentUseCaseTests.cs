using System;
using System.Collections.Generic;
using System.Globalization;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class UpdateStudentUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IStudentOwnershipGuard> _mockOwnershipGuard;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Mock<ILogger<UpdateStudentUseCase>> _mockLogger;

    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _fixedTime = new(2025, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    public UpdateStudentUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(t => t.UserId).Returns(_userId);
        _mockTenantContext.Setup(t => t.Role).Returns(UserRole.CenterManager.ToString());

        _mockOwnershipGuard = new Mock<IStudentOwnershipGuard>();
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_fixedTime));

        _mockLogger = new Mock<ILogger<UpdateStudentUseCase>>();
    }

    private EduTwinDbContext CreateContext(string dbName, Guid? centerId = null, params IInterceptor[] interceptors)
    {
        var tenantId = centerId ?? _centerId;
        var tenantContextMock = new Mock<ITenantIdAccessor>();
        tenantContextMock.Setup(t => t.CenterId).Returns(tenantId);

        var optionsBuilder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName);

        if (interceptors != null && interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        return new EduTwinDbContext(optionsBuilder.Options, tenantContextMock.Object);
    }

    private async Task SeedCenterAsync(EduTwinDbContext context, Guid centerId, CenterStatus status = CenterStatus.Active, bool isDeleted = false)
    {
        var centerSuffix = centerId.ToString("N")[..8];
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterName = "Test Center",
            CenterCode = $"TC_{centerSuffix}",
            Timezone = "UTC",
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime
        });
        await context.SaveChangesAsync();
    }

    private async Task<Class> SeedClassAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid? teacherId = null,
        ClassStatus status = ClassStatus.Active,
        bool isDeleted = false)
    {
        var subjectId = Guid.NewGuid();
        var subjectSuffix = subjectId.ToString("N")[..8];
        context.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = $"SUB_{subjectSuffix}", SubjectName = "S1", IsActive = true, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });

        var tId = teacherId ?? Guid.NewGuid();
        if (!context.Teachers.Any(t => t.TeacherId == tId))
        {
            var teacherSuffix = tId.ToString("N")[..8];
            context.Users.Add(new User { UserId = tId, CenterId = centerId, Username = $"teacher_{teacherSuffix}", PasswordHash = "h", DisplayName = "t1", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
            context.Teachers.Add(new Teacher { TeacherId = tId, CenterId = centerId, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
        }

        var cls = new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = centerId,
            ClassName = "C1",
            AcademicYear = "2025",
            TeacherId = tId,
            SubjectId = subjectId,
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            RowVersion = 1
        };
        context.Classes.Add(cls);
        await context.SaveChangesAsync();
        return cls;
    }

    private async Task<Student> SeedStudentAsync(
        EduTwinDbContext context,
        Guid centerId,
        bool isUserSoftDeleted = false,
        bool isStudentSoftDeleted = false,
        string role = "Student",
        UserStatus status = UserStatus.Active,
        ulong rowVersion = 1)
    {
        var studentId = Guid.NewGuid();
        var studentSuffix = studentId.ToString("N")[..8];

        var user = new User
        {
            UserId = studentId,
            CenterId = centerId,
            Username = $"student_{studentSuffix}",
            PasswordHash = "h",
            DisplayName = "d",
            RoleName = Enum.TryParse<UserRole>(role, out var r) ? r : UserRole.Student,
            Status = status,
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            IsDeleted = isUserSoftDeleted,
            RowVersion = rowVersion
        };

        var student = new Student
        {
            StudentId = studentId,
            CenterId = centerId,
            FullName = "d",
            GradeLevel = 10,
            DateOfBirth = new DateOnly(2010, 1, 1),
            CreatedAt = _fixedTime,
            UpdatedAt = _fixedTime,
            IsDeleted = isStudentSoftDeleted,
            RowVersion = rowVersion,
            User = user
        };

        context.Users.Add(user);
        context.Students.Add(student);
        await context.SaveChangesAsync();

        return student;
    }

    private UpdateStudentRequest CreateValidRequest(string rowVersion = "1") => new()
    {
        FullName = "Updated Name",
        GradeLevel = 11,
        Status = UserStatus.Active,
        RowVersion = rowVersion
    };

    [Fact]
    public async Task T01_CenterManager_SameTenantUpdate_Success()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", result.Data!.FullName);
    }

    [Fact]
    public async Task T02_Teacher_OwnershipAllowed_Success()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        _mockTenantContext.Setup(t => t.Role).Returns(UserRole.Teacher.ToString());

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Name", result.Data!.FullName);
    }

    [Fact]
    public async Task T03_Teacher_OwnershipForbidden_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        _mockTenantContext.Setup(t => t.Role).Returns(UserRole.Teacher.ToString());
        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Forbidden);

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task T04_OwnershipNotFound_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.NotFound);

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T05_UndefinedOwnershipDecision_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        _mockOwnershipGuard.Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OwnershipDecision)999);

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T06_StudentRole_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        _mockTenantContext.Setup(t => t.Role).Returns(UserRole.Student.ToString());

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("admin")]
    [InlineData("0")]
    [InlineData(null)]
    public async Task T07_InvalidRole_FailsClosed(string? role)
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        _mockTenantContext.Setup(t => t.Role).Returns(role!);

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T08_InvalidTenantContext_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        _mockTenantContext.Setup(t => t.IsResolved).Returns(false);

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T09_CenterMissing_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var student = await SeedStudentAsync(context, _centerId);

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T10_StudentGuidEmpty_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.Empty, CreateValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T11_CrossTenantStudent_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var otherCenterId = Guid.NewGuid();
        var contextB = CreateContext(dbName, otherCenterId);
        await SeedCenterAsync(contextB, otherCenterId);
        var student = await SeedStudentAsync(contextB, otherCenterId);

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T12_StudentSoftDeleted_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId, isStudentSoftDeleted: true);

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T13_UserSoftDeleted_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId, isUserSoftDeleted: true);

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T14_UserRoleNotStudent_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId, role: "Teacher");

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, CreateValidRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T15_InvalidValidation_ReturnsValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        var request = CreateValidRequest();
        request.FullName = ""; // invalid

        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task T16_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        student.FullName = "Modified";
        await context.SaveChangesAsync();

        var request = CreateValidRequest("1");
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task T17_DbUpdateConcurrencyException_ReturnsConcurrencyConflict()
    {
        bool shouldThrow = false;
        var interceptor = new ThrowingSaveChangesInterceptor(ctx =>
        {
            if (shouldThrow) throw new DbUpdateConcurrencyException("Conflict");
        });

        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, null, interceptor);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        shouldThrow = true;
        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged && e.State != EntityState.Detached);
    }

    [Fact]
    public async Task T18_UnrelatedDbUpdateException_Rethrows()
    {
        bool shouldThrow = false;
        var interceptor = new ThrowingSaveChangesInterceptor(ctx =>
        {
            if (shouldThrow) throw new DbUpdateException("Other error");
        });

        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, null, interceptor);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        shouldThrow = true;
        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(student.StudentId, request));
    }

    [Fact]
    public async Task T19_ConcurrencyFailure_NoPartialUpdate()
    {
        bool shouldThrow = false;
        var interceptor = new ThrowingSaveChangesInterceptor(ctx =>
        {
            if (shouldThrow) throw new DbUpdateConcurrencyException("Conflict");
        });

        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, null, interceptor);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        shouldThrow = true;
        var request = CreateValidRequest();
        request.FullName = "PARTIAL";
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        await sut.ExecuteAsync(student.StudentId, request);

        shouldThrow = false;
        var finalContext = CreateContext(dbName);
        var dbStudent = await finalContext.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.StudentId == student.StudentId);
        Assert.NotEqual("PARTIAL", dbStudent!.FullName);
        Assert.NotEqual("PARTIAL", dbStudent.User.DisplayName);
    }

    [Fact]
    public async Task T20_ExactRowVersionIncrement()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId, rowVersion: 5);

        var request = CreateValidRequest(student.RowVersion.ToString());
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        if (!result.IsSuccess) throw new Exception($"T20 Failed! ErrorCode: {result.ErrorCode}");

        Assert.True(result.IsSuccess);
        Assert.Equal("2", result.Data!.RowVersion);
    }

    [Fact]
    public async Task T21_TimeProvider_FixedTimeAndActor()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        // Update fixed time
        var newFixedTime = _fixedTime.AddDays(1);
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(newFixedTime));

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        var finalContext = CreateContext(dbName);
        var dbStudent = await finalContext.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.StudentId == student.StudentId);
        Assert.Equal(newFixedTime, dbStudent!.UpdatedAt);
        Assert.Equal(_userId, dbStudent.UpdatedBy);
        Assert.Equal(newFixedTime, dbStudent.User.UpdatedAt);
        Assert.Equal(_userId, dbStudent.User.UpdatedBy);
    }

    [Fact]
    public async Task T22_UnchangedFields_User()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);
        var oldHash = student.User.PasswordHash;
        var oldRole = student.User.RoleName;

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        await sut.ExecuteAsync(student.StudentId, request);

        var finalContext = CreateContext(dbName);
        var dbStudent = await finalContext.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.StudentId == student.StudentId);
        Assert.Equal(oldHash, dbStudent!.User.PasswordHash);
        Assert.Equal(oldRole, dbStudent.User.RoleName);
    }

    [Fact]
    public async Task T23_UnchangedFields_Student()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);
        var oldDob = student.DateOfBirth;
        var oldCenterId = student.CenterId;

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        await sut.ExecuteAsync(student.StudentId, request);

        var finalContext = CreateContext(dbName);
        var dbStudent = await finalContext.Students.FirstOrDefaultAsync(s => s.StudentId == student.StudentId);
        Assert.Equal(oldDob, dbStudent!.DateOfBirth);
        Assert.Equal(oldCenterId, dbStudent.CenterId);
    }

    [Fact]
    public async Task T24_UnchangedFields_Navigations()
    {
        // Asserting that navigating collections aren't cleared
        // Not specifically loading them, just proving they aren't explicitly deleted
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task T25_ActiveClassCount_CenterManager()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId);
        var c2 = await SeedClassAsync(context, _centerId);

        context.ChangeTracker.Clear();
        context.ClassStudents.Add(new ClassStudent { ClassId = c1.ClassId, StudentId = student.StudentId, Status = ClassStudentStatus.Active, CenterId = _centerId, JoinedAt = _fixedTime });
        context.ClassStudents.Add(new ClassStudent { ClassId = c2.ClassId, StudentId = student.StudentId, Status = ClassStudentStatus.Active, CenterId = _centerId, JoinedAt = _fixedTime });
        await context.SaveChangesAsync();

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.ActiveClassCount);
    }

    [Fact]
    public async Task T26_ActiveClassCount_Teacher_OwnClassOnly()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);
        var ownClass = await SeedClassAsync(context, _centerId, teacherId: _userId);
        var otherClass = await SeedClassAsync(context, _centerId);

        context.ChangeTracker.Clear();
        context.ClassStudents.Add(new ClassStudent { ClassId = ownClass.ClassId, StudentId = student.StudentId, Status = ClassStudentStatus.Active, CenterId = _centerId, JoinedAt = _fixedTime });
        context.ClassStudents.Add(new ClassStudent { ClassId = otherClass.ClassId, StudentId = student.StudentId, Status = ClassStudentStatus.Active, CenterId = _centerId, JoinedAt = _fixedTime });
        await context.SaveChangesAsync();

        _mockTenantContext.Setup(t => t.Role).Returns(UserRole.Teacher.ToString());

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ActiveClassCount);
    }

    [Fact]
    public async Task T27_ActiveClassCount_RemovedMembership()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId);

        context.ChangeTracker.Clear();
        context.ClassStudents.Add(new ClassStudent { ClassId = c1.ClassId, StudentId = student.StudentId, Status = ClassStudentStatus.Removed, CenterId = _centerId, JoinedAt = _fixedTime });
        await context.SaveChangesAsync();

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.ActiveClassCount);
    }

    [Fact]
    public async Task T28_ActiveClassCount_ArchivedClass()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId, status: ClassStatus.Archived);

        context.ChangeTracker.Clear();
        context.ClassStudents.Add(new ClassStudent { ClassId = c1.ClassId, StudentId = student.StudentId, Status = ClassStudentStatus.Active, CenterId = _centerId, JoinedAt = _fixedTime });
        await context.SaveChangesAsync();

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.ActiveClassCount);
    }

    [Fact]
    public async Task T29_Response_UsesStudentDto()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId, rowVersion: 10);

        var request = CreateValidRequest(student.RowVersion.ToString());
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(student.StudentId, result.Data.StudentId);
        Assert.Equal(student.User.Username, result.Data.Username);
        Assert.Equal(UserStatus.Active.ToString(), result.Data.Status);
    }

    [Fact]
    public void T30_DI_Resolution_Test()
    {
        var services = new ServiceCollection();
        services.AddScoped<EduTwinDbContext>(sp => CreateContext(Guid.NewGuid().ToString()));
        services.AddScoped(sp => _mockTenantContext.Object);
        services.AddScoped(sp => _mockOwnershipGuard.Object);
        services.AddScoped(sp => _mockTimeProvider.Object);
        services.AddLogging();

        // Target method
        services.AddScoped<IUpdateStudentUseCase, UpdateStudentUseCase>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<IUpdateStudentUseCase>();
        Assert.NotNull(useCase);
        Assert.IsType<UpdateStudentUseCase>(useCase);
    }

    private class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly Action<EduTwinDbContext> _onSaving;
        public ThrowingSaveChangesInterceptor(Action<EduTwinDbContext> onSaving) => _onSaving = onSaving;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            _onSaving((EduTwinDbContext)eventData.Context!);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            _onSaving((EduTwinDbContext)eventData.Context!);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
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
    public async Task T05b_InvalidRole_ReturnsResourceNotFound(string? role)
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        _mockTenantContext.Setup(t => t.Role).Returns(role);

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task T17_RowVersionZero_ReturnsValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        var request = CreateValidRequest("0");
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(student.StudentId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task T30_CancellationToken_PassedToOwnershipGuard()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var student = await SeedStudentAsync(context, _centerId);

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var request = CreateValidRequest();
        var sut = new UpdateStudentUseCase(context, _mockTenantContext.Object, _mockOwnershipGuard.Object, _mockTimeProvider.Object, _mockLogger.Object);

        await sut.ExecuteAsync(student.StudentId, request, token);

        _mockOwnershipGuard.Verify(g => g.CheckStudentAccessAsync(student.StudentId, token), Times.Once);
    }
}
