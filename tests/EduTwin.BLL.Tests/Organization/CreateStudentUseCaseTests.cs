using System;
using System.Collections.Generic;
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
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class CreateStudentUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IPasswordHasher<User>> _mockPasswordHasher;
    private readonly Mock<ILogger<CreateStudentUseCase>> _mockLogger;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly DateTime _fixedTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public CreateStudentUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(t => t.UserId).Returns(_actorId);
        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        _mockPasswordHasher = new Mock<IPasswordHasher<User>>();
        _mockPasswordHasher.Setup(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
                           .Returns("hashed_password");

        _mockLogger = new Mock<ILogger<CreateStudentUseCase>>();

        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(_fixedTime));
    }

    private EduTwinDbContext CreateContext(string dbName, Guid? tenantId = null, params IInterceptor[] interceptors)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));

        if (interceptors != null && interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(tenantId ?? _centerId);

        return new EduTwinDbContext(optionsBuilder.Options, mockAccessor.Object);
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

    private CreateStudentRequest CreateValidRequest(params Guid[] classIds) => new()
    {
        Username = "student.new",
        TemporaryPassword = "securePassword123",
        FullName = "New Student",
        GradeLevel = 10,
        ClassIds = classIds.ToList()
    };

    [Fact]
    public async Task Success_UsesTimeProviderForEveryTimestamp()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var cls = await SeedClassAsync(context, _centerId);

        var request = CreateValidRequest(cls.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);

        var studentId = Guid.Parse(result.Data!.StudentId.ToString());

        var user = await context.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == studentId);
        Assert.Equal(_fixedTime, user.CreatedAt);
        Assert.Equal(_fixedTime, user.UpdatedAt);

        var student = await context.Students.IgnoreQueryFilters().FirstAsync(s => s.StudentId == studentId);
        Assert.Equal(_fixedTime, student.CreatedAt);
        Assert.Equal(_fixedTime, student.UpdatedAt);

        var classStudent = await context.ClassStudents.IgnoreQueryFilters().FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(_fixedTime, classStudent.JoinedAt);

        var twin = await context.StudentTwins.IgnoreQueryFilters().FirstAsync(st => st.StudentId == studentId);
        Assert.Equal(_fixedTime, twin.CreatedAt);
        Assert.Equal(_fixedTime, twin.UpdatedAt);
    }

    [Fact]
    public async Task Response_UsesStudentDtoContract()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var cls = await SeedClassAsync(context, _centerId);

        var request = CreateValidRequest(cls.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.IsType<StudentDto>(result.Data);
        Assert.Equal(request.Username, result.Data.Username);
        Assert.Equal(request.FullName, result.Data.FullName);
        Assert.Equal(request.GradeLevel, result.Data.GradeLevel);
        Assert.Equal(UserStatus.Active.ToString(), result.Data.Status);
        Assert.Equal(1, result.Data.ActiveClassCount);
        Assert.NotEmpty(result.Data.RowVersion);
    }

    [Fact]
    public async Task ClassIds_OmittedOrNull_FailsValidation()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);

        var request = CreateValidRequest();
        request.ClassIds = null;

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ClassIds_ExplicitEmptyArray_PassesAndCreatesStudentWithoutMembership()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.ActiveClassCount);

        var studentId = Guid.Parse(result.Data.StudentId.ToString());
        var memberships = await context.ClassStudents.IgnoreQueryFilters().Where(cs => cs.StudentId == studentId).ToListAsync();
        Assert.Empty(memberships);

        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);
        Assert.NotNull(student);
        Assert.Equal(request.FullName, student.FullName);

        var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == studentId);
        Assert.NotNull(user);
        Assert.Equal(UserRole.Student, user.RoleName);

        var studentTwin = await context.StudentTwins.FirstOrDefaultAsync(st => st.StudentId == studentId);
        Assert.NotNull(studentTwin);
        Assert.Equal(0, studentTwin.OverallMastery);
    }

    [Fact]
    public async Task CenterManager_CreateStudent_NoClasses_Success()
    {
        // Aliased to ExplicitEmptyArray test to ensure regression parity
        await ClassIds_ExplicitEmptyArray_PassesAndCreatesStudentWithoutMembership();
    }

    [Fact]
    public async Task Request_NormalizesUsernameAndFullName_ButPreservesPassword()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = new CreateStudentRequest
        {
            Username = "  student.raw  ",
            FullName = "  Raw Name  ",
            TemporaryPassword = "  password  ",
            GradeLevel = 10,
            ClassIds = new List<Guid>()
        };

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("student.raw", result.Data!.Username);
        Assert.Equal("Raw Name", result.Data.FullName);

        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), "  password  "), Times.Once);
    }

    [Fact]
    public async Task WhitespaceOnlyPassword_ValidationFailedAndHasherNotCalled()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.TemporaryPassword = "                 ";

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CenterDeleted_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId, isDeleted: true);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CenterSuspended_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId, status: CenterStatus.Suspended);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CenterMissing_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task InvalidTenant_ReturnsResourceNotFound_PasswordHasherNotCalled()
    {
        _mockTenantContext.Setup(t => t.IsResolved).Returns(false);

        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);

        Assert.Empty(await context.Users.IgnoreQueryFilters().ToListAsync());
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("teacher")]
    [InlineData("Admin")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task InvalidRole_FailsClosed(string? role)
    {
        _mockTenantContext.Setup(t => t.Role).Returns(role!);

        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);

        Assert.Empty(await context.Users.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task PreValidationFailure_DoesNotCallPasswordHasher()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.Username = ""; // Invalid

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PasswordHasher_ReceivesRawPassword()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.TemporaryPassword = "  password  ";
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        await sut.ExecuteAsync(request);

        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), "  password  "), Times.Once);
    }

    [Fact]
    public async Task CenterManager_CreateStudent_MultipleClasses_Success()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId);
        var c2 = await SeedClassAsync(context, _centerId);

        var request = CreateValidRequest(c1.ClassId, c2.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.ActiveClassCount);

        var memberships = await context.ClassStudents.Where(cs => cs.StudentId == result.Data.StudentId).ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.Contains(memberships, m => m.ClassId == c1.ClassId);
        Assert.Contains(memberships, m => m.ClassId == c2.ClassId);
        Assert.All(memberships, m => Assert.Equal(ClassStudentStatus.Active, m.Status));
        Assert.All(memberships, m => Assert.Equal(_actorId, m.CreatedBy));
        Assert.All(memberships, m => Assert.Equal(_fixedTime, m.JoinedAt));
    }

    [Fact]
    public async Task Teacher_CreateStudent_OwnClass_Success()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId, teacherId: _actorId);

        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));

        var request = CreateValidRequest(c1.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ActiveClassCount);
    }

    [Fact]
    public async Task Teacher_CreateStudent_OtherTeacherClass_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var otherTeacherId = Guid.NewGuid();
        var c1 = await SeedClassAsync(context, _centerId, teacherId: otherTeacherId);

        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));

        var request = CreateValidRequest(c1.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        Assert.Empty(await context.Students.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task CrossTenantClass_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var otherCenterId = Guid.NewGuid();
        var contextB = CreateContext(dbName, otherCenterId);
        await SeedCenterAsync(contextB, otherCenterId);
        var c1 = await SeedClassAsync(contextB, otherCenterId);

        var classB = await contextB.Classes.FirstOrDefaultAsync(c => c.ClassId == c1.ClassId);
        Assert.NotNull(classB);
        Assert.Equal(otherCenterId, classB.CenterId);

        var request = CreateValidRequest(c1.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Empty(await context.Students.IgnoreQueryFilters().ToListAsync());
        Assert.DoesNotContain(context.ChangeTracker.Entries<User>(), e => e.Entity.RoleName == UserRole.Student);
        Assert.DoesNotContain(context.ChangeTracker.Entries<ClassStudent>(), e => e.State == EntityState.Added);
        Assert.DoesNotContain(context.ChangeTracker.Entries<StudentTwin>(), e => e.State == EntityState.Added);
    }

    [Fact]
    public async Task MissingClass_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId);

        var request = CreateValidRequest(c1.ClassId, Guid.NewGuid());
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ArchivedClass_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId, status: ClassStatus.Archived);

        var request = CreateValidRequest(c1.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedClass_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId, isDeleted: true);

        var request = CreateValidRequest(c1.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DuplicateUsername_SameTenant_ReturnsDuplicateResource()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        context.Users.Add(new User { UserId = Guid.NewGuid(), CenterId = _centerId, Username = "student.new", PasswordHash = "h", DisplayName = "d", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
        await context.SaveChangesAsync();

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task DuplicateUsername_CrossTenant_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var otherCenterId = Guid.NewGuid();
        var contextB = CreateContext(dbName, otherCenterId);
        await SeedCenterAsync(contextB, otherCenterId);
        contextB.Users.Add(new User { UserId = Guid.NewGuid(), CenterId = otherCenterId, Username = "student.new", PasswordHash = "h", DisplayName = "d", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
        await contextB.SaveChangesAsync();

        var userB = await contextB.Users.FirstOrDefaultAsync(u => u.Username == "student.new");
        Assert.NotNull(userB);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
    }

    private class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly Action<EduTwinDbContext> _onSaving;
        public ThrowingSaveChangesInterceptor(Action<EduTwinDbContext> onSaving) => _onSaving = onSaving;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is EduTwinDbContext db)
            {
                _onSaving(db);
            }
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is EduTwinDbContext db)
            {
                _onSaving(db);
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task ConcurrentDuplicateUsername_AfterPrecheck_ReturnsDuplicateResource()
    {
        var dbName = Guid.NewGuid().ToString();
        bool shouldThrow = false;

        var interceptor = new ThrowingSaveChangesInterceptor(db =>
        {
            if (shouldThrow && db.ChangeTracker.Entries<User>().Any(e => e.State == EntityState.Added))
            {
                var optionsBuilder = new DbContextOptionsBuilder<EduTwinDbContext>()
                    .UseInMemoryDatabase(databaseName: dbName)
                    .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));

                using var freshContext = new EduTwinDbContext(optionsBuilder.Options);

                freshContext.Users.Add(new User
                {
                    UserId = Guid.NewGuid(),
                    CenterId = _centerId,
                    Username = "student.new",
                    PasswordHash = "h",
                    DisplayName = "d",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Active,
                    CreatedAt = _fixedTime,
                    UpdatedAt = _fixedTime
                });
                freshContext.SaveChanges();

                throw new DbUpdateException("Simulated unique constraint failure");
            }
        });

        var context = CreateContext(dbName, null, interceptor);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);

        shouldThrow = true;
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);

        Assert.DoesNotContain(context.ChangeTracker.Entries<User>(), e => e.State == EntityState.Added);
        Assert.DoesNotContain(context.ChangeTracker.Entries<Student>(), e => e.State == EntityState.Added);
        Assert.DoesNotContain(context.ChangeTracker.Entries<ClassStudent>(), e => e.State == EntityState.Added);
        Assert.DoesNotContain(context.ChangeTracker.Entries<StudentTwin>(), e => e.State == EntityState.Added);

        var optionsBuilder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
        using var finalContext = new EduTwinDbContext(optionsBuilder.Options);

        var allUsers = await finalContext.Users.IgnoreQueryFilters().ToListAsync();
        Assert.Single(allUsers);
        Assert.Empty(await finalContext.Students.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await finalContext.ClassStudents.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await finalContext.StudentTwins.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task UnrelatedDbUpdateException_Rethrows()
    {
        var dbName = Guid.NewGuid().ToString();
        bool shouldThrow = false;
        var interceptor = new ThrowingSaveChangesInterceptor(db =>
        {
            if (shouldThrow && db.ChangeTracker.Entries<User>().Any(e => e.State == EntityState.Added))
            {
                throw new DbUpdateException("Unrelated DB error");
            }
        });

        var context = CreateContext(dbName, null, interceptor);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.ClassIds = new List<Guid>();

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);

        shouldThrow = true;
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(request));
        Assert.Equal("Unrelated DB error", ex.Message);

        var optionsBuilder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
        using var finalContext = new EduTwinDbContext(optionsBuilder.Options);

        Assert.Empty(await finalContext.Users.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await finalContext.Students.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await finalContext.ClassStudents.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await finalContext.StudentTwins.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task SaveFailure_PersistsNoPartialAggregate()
    {
        var dbName = Guid.NewGuid().ToString();
        bool shouldThrow = false;
        var interceptor = new ThrowingSaveChangesInterceptor(db =>
        {
            if (shouldThrow && db.ChangeTracker.Entries<User>().Any(e => e.State == EntityState.Added))
            {
                throw new Exception("General failure");
            }
        });

        var context = CreateContext(dbName, null, interceptor);
        await SeedCenterAsync(context, _centerId);
        var cls = await SeedClassAsync(context, _centerId);

        var request = CreateValidRequest(cls.ClassId);

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockTimeProvider.Object, _mockLogger.Object);

        shouldThrow = true;
        await Assert.ThrowsAsync<Exception>(() => sut.ExecuteAsync(request));

        var optionsBuilder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
        using var finalContext = new EduTwinDbContext(optionsBuilder.Options);

        var users = await finalContext.Users.IgnoreQueryFilters().ToListAsync();
        Assert.Single(users);
        Assert.Empty(await finalContext.Students.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await finalContext.ClassStudents.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await finalContext.StudentTwins.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public void DI_Resolution_Test()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<EduTwinDbContext>(options => options.UseInMemoryDatabase("DI"));
        services.AddScoped<ITenantContext>(sp => _mockTenantContext.Object);
        services.AddScoped<IPasswordHasher<User>>(sp => _mockPasswordHasher.Object);
        services.AddSingleton(TimeProvider.System);

        services.AddOrganization();

        var provider = services.BuildServiceProvider();
        var useCase = provider.GetRequiredService<ICreateStudentUseCase>();

        Assert.NotNull(useCase);
        Assert.IsType<CreateStudentUseCase>(useCase);
    }
}
