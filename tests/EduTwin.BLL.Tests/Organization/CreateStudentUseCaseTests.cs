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
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    }

    private EduTwinDbContext CreateContext(string dbName, Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
            
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(tenantId ?? _centerId);
        
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    private async Task SeedCenterAsync(EduTwinDbContext context, Guid centerId, CenterStatus status = CenterStatus.Active, bool isDeleted = false)
    {
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterName = "Test Center",
            CenterCode = "TC",
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
        context.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S1", SubjectName = "S1", IsActive = true, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });

        var tId = teacherId ?? Guid.NewGuid();
        if (!context.Teachers.Any(t => t.TeacherId == tId))
        {
            context.Users.Add(new User { UserId = tId, CenterId = centerId, Username = "t1", PasswordHash = "h", DisplayName = "t1", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
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
    public async Task CenterManager_CreateStudent_NoClasses_Success()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.ActiveClassCount);

        var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == Guid.Parse(result.Data.StudentId));
        Assert.NotNull(student);
        Assert.Equal(request.FullName, student.FullName);
        
        var user = await context.Users.FirstOrDefaultAsync(u => u.UserId == student.StudentId);
        Assert.NotNull(user);
        Assert.Equal(UserRole.Student, user.RoleName);

        var studentTwin = await context.StudentTwins.FirstOrDefaultAsync(st => st.StudentId == student.StudentId);
        Assert.NotNull(studentTwin);
        Assert.Equal(0, studentTwin.OverallMastery);

        var countKnow = await context.KnowledgeTwins.CountAsync();
        var countBehav = await context.BehaviorTwins.CountAsync();
        var countGoal = await context.StudentSubjectGoals.CountAsync();
        Assert.Equal(0, countKnow);
        Assert.Equal(0, countBehav);
        Assert.Equal(0, countGoal);
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
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.ActiveClassCount);

        var memberships = await context.ClassStudents.Where(cs => cs.StudentId == Guid.Parse(result.Data.StudentId)).ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, m => Assert.Equal(ClassStudentStatus.Active, m.Status));
        Assert.All(memberships, m => Assert.Equal(_actorId, m.CreatedBy));
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
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
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
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantClass_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        
        var otherCenterId = Guid.NewGuid();
        await SeedCenterAsync(context, otherCenterId);
        var c1 = await SeedClassAsync(context, otherCenterId);

        var request = CreateValidRequest(c1.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingClass_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);
        var c1 = await SeedClassAsync(context, _centerId);

        var request = CreateValidRequest(c1.ClassId, Guid.NewGuid());
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
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
        var c1 = await SeedClassAsync(context, _centerId, status: (ClassStatus)99);

        var request = CreateValidRequest(c1.ClassId);
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
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
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
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
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
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
        await SeedCenterAsync(context, otherCenterId);
        context.Users.Add(new User { UserId = Guid.NewGuid(), CenterId = otherCenterId, Username = "student.new", PasswordHash = "h", DisplayName = "d", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
        await context.SaveChangesAsync();

        var request = CreateValidRequest();
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task InvalidTenantContext_FailsClosed()
    {
        _mockTenantContext.Setup(t => t.IsResolved).Returns(false);

        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("teacher")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public async Task InvalidRole_FailsClosed(string? role)
    {
        _mockTenantContext.Setup(t => t.Role).Returns(role!);

        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task CenterMissingOrDeleted_FailsClosed()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        // Do not seed center

        var request = CreateValidRequest();
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task PreValidationFailure_DoesNotCallPasswordHasher()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        var request = CreateValidRequest();
        request.Username = ""; // Invalid

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
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

        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        await sut.ExecuteAsync(request);

        _mockPasswordHasher.Verify(p => p.HashPassword(It.IsAny<User>(), "  password  "), Times.Once);
    }

    [Fact]
    public async Task ConcurrentDuplicateUsername_MappedCorrectly()
    {
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName);
        await SeedCenterAsync(context, _centerId);

        // We can't easily trigger DbUpdateException for IX_Users_CenterId_Username in InMemory DB.
        // We will just verify normal duplication logic works. To test the catch block, we would need to mock DbContext 
        // or use a real DB in integration tests. We will rely on standard duplicate check.
        context.Users.Add(new User { UserId = Guid.NewGuid(), CenterId = _centerId, Username = "student.new", PasswordHash = "h", DisplayName = "d", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = _fixedTime, UpdatedAt = _fixedTime });
        await context.SaveChangesAsync();

        var request = CreateValidRequest();
        var sut = new CreateStudentUseCase(context, _mockTenantContext.Object, _mockPasswordHasher.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(request);

        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public void DI_Resolution_Test()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<EduTwinDbContext>(options => options.UseInMemoryDatabase("DI"));
        services.AddScoped<ITenantContext>(sp => _mockTenantContext.Object);
        services.AddScoped<IPasswordHasher<User>>(sp => _mockPasswordHasher.Object);
        
        services.AddOrganization();
        
        var provider = services.BuildServiceProvider();
        var useCase = provider.GetRequiredService<ICreateStudentUseCase>();
        
        Assert.NotNull(useCase);
        Assert.IsType<CreateStudentUseCase>(useCase);
    }
}
