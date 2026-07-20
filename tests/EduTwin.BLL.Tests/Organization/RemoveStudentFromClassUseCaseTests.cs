using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EduTwin.BLL.Organization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.DAL.Assignments;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Tests.Organization;

public class TestSaveChangesInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
{
    public int SaveChangesCount { get; private set; }
    public CancellationToken LastToken { get; private set; }

    public void Reset()
    {
        SaveChangesCount = 0;
        LastToken = default;
    }

    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
        Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        LastToken = cancellationToken;
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

public class RemoveStudentFromClassUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Mock<IClassOwnershipGuard> _mockOwnershipGuard;
    private readonly Mock<ILogger<RemoveStudentFromClassUseCase>> _mockLogger;
    private static readonly DateTime SeedTimeUtc = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);
    private readonly TestSaveChangesInterceptor _interceptor;

    public RemoveStudentFromClassUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.UserId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(SeedTimeUtc);

        _mockOwnershipGuard = new Mock<IClassOwnershipGuard>();
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OwnershipDecision.Allowed);

        _mockLogger = new Mock<ILogger<RemoveStudentFromClassUseCase>>();
        _interceptor = new TestSaveChangesInterceptor();
    }

    private EduTwinDbContext CreateContext(string dbName, Guid centerId)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(_interceptor)
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(a => a.CenterId).Returns(centerId);

        return new EduTwinDbContext(options, tenantAccessorMock.Object);
    }

    private async Task SeedBaseDataAsync(EduTwinDbContext context, Guid centerId, Guid classId, Guid studentId, string centerCode = "C1")
    {
        context.Centers.Add(new Center { CenterId = centerId, CenterName = "Center " + centerCode, CenterCode = centerCode, Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        var teacherId = Guid.NewGuid();
        var teacherUser = new User { UserId = teacherId, CenterId = centerId, DisplayName = "Teacher", RoleName = UserRole.Teacher, Username = "T_" + centerCode, PasswordHash = "H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc };
        context.Users.Add(teacherUser);
        context.Teachers.Add(new Teacher { TeacherId = teacherId, CenterId = centerId, User = teacherUser, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        var subjectId = Guid.NewGuid();
        context.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectName = "Subject", SubjectCode = "S_" + centerCode, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        context.Classes.Add(new Class { ClassId = classId, CenterId = centerId, ClassName = "Class " + centerCode, AcademicYear = "2026", SubjectId = subjectId, TeacherId = teacherId, CreatedAt = SeedTimeUtc, CreatedBy = Guid.NewGuid(), UpdatedAt = SeedTimeUtc, UpdatedBy = Guid.NewGuid(), Status = EduTwin.Contracts.Organization.ClassStatus.Active, RowVersion = 1 });

        var user = new User { UserId = studentId, CenterId = centerId, DisplayName = "Student", RoleName = UserRole.Student, Username = "S_" + centerCode, PasswordHash = "H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc };
        context.Users.Add(user);
        context.Students.Add(new Student { StudentId = studentId, CenterId = centerId, GradeLevel = 10, FullName = "Student", User = user, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task EmptyClassId_ResourceNotFound_NoGuardOrPersistence()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        _interceptor.Reset();
        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.Empty, Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(0, _interceptor.SaveChangesCount);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    }

    [Fact]
    public async Task EmptyStudentId_ResourceNotFound_NoGuardOrPersistence()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        _interceptor.Reset();
        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.Empty);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(0, _interceptor.SaveChangesCount);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    }

    [Theory]
    [InlineData(false, true, true, true, true, "CenterManager")]
    [InlineData(true, false, true, true, true, "CenterManager")]
    [InlineData(true, true, false, true, true, "CenterManager")]
    [InlineData(true, true, true, false, true, "CenterManager")]
    [InlineData(true, true, true, true, false, "CenterManager")]
    public async Task InvalidTenantContexts_ResourceNotFound(bool resolved, bool centerIdHasValue, bool centerIdNotEmpty, bool userIdHasValue, bool userIdNotEmpty, string role)
    {
        var tenantMock = new Mock<ITenantContext>();
        tenantMock.Setup(t => t.IsResolved).Returns(resolved);
        tenantMock.Setup(t => t.CenterId).Returns(centerIdHasValue ? (centerIdNotEmpty ? Guid.NewGuid() : Guid.Empty) : null);
        tenantMock.Setup(t => t.UserId).Returns(userIdHasValue ? (userIdNotEmpty ? Guid.NewGuid() : Guid.Empty) : null);
        tenantMock.Setup(t => t.Role).Returns(role);

        var sut = new RemoveStudentFromClassUseCase(CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid()), tenantMock.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("teacher")]
    [InlineData("Admin")]
    [InlineData("1")]
    [InlineData(null)]
    public async Task InvalidRoles_ResourceNotFound(string? role)
    {
        var tenantMock = new Mock<ITenantContext>();
        tenantMock.Setup(t => t.IsResolved).Returns(true);
        tenantMock.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        tenantMock.Setup(t => t.UserId).Returns(Guid.NewGuid());
        tenantMock.Setup(t => t.Role).Returns(role);

        var sut = new RemoveStudentFromClassUseCase(CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid()), tenantMock.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task OwnershipForbidden_ForbiddenResource()
    {
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Forbidden);
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, Guid.NewGuid(), Guid.NewGuid());
        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task OwnershipNotFound_ResourceNotFound()
    {
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.NotFound);
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, Guid.NewGuid(), Guid.NewGuid());
        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UndefinedOwnershipDecision_ResourceNotFound()
    {
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OwnershipDecision)999);
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, Guid.NewGuid(), Guid.NewGuid());
        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(EduTwin.Contracts.Organization.CenterStatus.Suspended)]
    [InlineData((EduTwin.Contracts.Organization.CenterStatus)99)] // Also test undefined center status
    public async Task CenterSuspended_ReturnsResourceNotFound(EduTwin.Contracts.Organization.CenterStatus status)
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await SeedBaseDataAsync(context, centerId, classId, studentId);
        var center = await context.Centers.FirstAsync();
        center.Status = status;
        await context.SaveChangesAsync();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CenterDeleted_ReturnsResourceNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        await SeedBaseDataAsync(context, centerId, classId, studentId);
        var center = await context.Centers.FirstAsync();
        center.IsDeleted = true;
        await context.SaveChangesAsync();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantClass_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var centerIdA = _mockTenantContext.Object.CenterId!.Value;
        var contextA = CreateContext(dbName, centerIdA);
        await SeedBaseDataAsync(contextA, centerIdA, Guid.NewGuid(), Guid.NewGuid(), "CA");

        var centerIdB = Guid.NewGuid();
        var contextB = CreateContext(dbName, centerIdB);
        var classIdB = Guid.NewGuid();
        var studentIdB = Guid.NewGuid();
        await SeedBaseDataAsync(contextB, centerIdB, classIdB, studentIdB, "CB");

        // Seed active membership in Center B so the test is not trivially passing due to missing membership
        contextB.ClassStudents.Add(new ClassStudent { CenterId = centerIdB, ClassId = classIdB, StudentId = studentIdB, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await contextB.SaveChangesAsync();

        // Precondition: contextB can see the membership
        var membershipVisibleInB = await contextB.ClassStudents.AnyAsync(cs => cs.CenterId == centerIdB && cs.ClassId == classIdB && cs.StudentId == studentIdB);
        Assert.True(membershipVisibleInB, "Precondition: Center B membership must exist in DB.");

        // Precondition: contextA cannot see the membership (tenant filter isolates it)
        var membershipVisibleInA = await contextA.ClassStudents.AnyAsync(cs => cs.ClassId == classIdB && cs.StudentId == studentIdB);
        Assert.False(membershipVisibleInA, "Precondition: Center A must not see Center B membership.");

        // Mock ownership Allowed to bypass guard and test the tenant-safe membership query
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new RemoveStudentFromClassUseCase(contextA, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classIdB, studentIdB);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantMembership_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var centerIdA = _mockTenantContext.Object.CenterId!.Value;
        var contextA = CreateContext(dbName, centerIdA);
        var classIdA = Guid.NewGuid();
        var studentIdA = Guid.NewGuid();
        await SeedBaseDataAsync(contextA, centerIdA, classIdA, studentIdA, "CA");

        var centerIdB = Guid.NewGuid();
        var contextB = CreateContext(dbName, centerIdB);
        var classIdB = Guid.NewGuid();
        var studentIdB = Guid.NewGuid();
        await SeedBaseDataAsync(contextB, centerIdB, classIdB, studentIdB, "CB");

        // Seed membership in Center B using Class B and Student B.
        contextB.ClassStudents.Add(new ClassStudent { CenterId = centerIdB, ClassId = classIdB, StudentId = studentIdB, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await contextB.SaveChangesAsync();

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        // Try to access from contextA with center A requesting class B/student B
        var sut = new RemoveStudentFromClassUseCase(contextA, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classIdB, studentIdB);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingMembership_ReturnsResourceNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId); // Membership not seeded

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ActiveMembership_BecomesRemoved()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);

        Assert.True(result.IsSuccess);
        var membership = await context.ClassStudents.FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(EduTwin.Contracts.Organization.ClassStudentStatus.Removed, membership.Status);
    }

    [Fact]
    public async Task ActiveMembership_SetsExactRemovedAtUtc()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc.AddDays(-1), Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        await sut.ExecuteAsync(classId, studentId);

        var membership = await context.ClassStudents.FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(SeedTimeUtc, membership.RemovedAt);
    }

    [Fact]
    public async Task ActiveMembership_PreservesJoinedAtAndCreatedBy()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        var originalJoinedAt = SeedTimeUtc.AddDays(-2);
        var originalCreatedBy = Guid.NewGuid();
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = originalJoinedAt, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = originalCreatedBy });
        await context.SaveChangesAsync();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        await sut.ExecuteAsync(classId, studentId);

        var membership = await context.ClassStudents.FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(originalJoinedAt, membership.JoinedAt);
        Assert.Equal(originalCreatedBy, membership.CreatedBy);
    }

    [Fact]
    public async Task ActiveMembership_RowStillExists_NoHardDelete()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        await sut.ExecuteAsync(classId, studentId);

        var count = await context.ClassStudents.CountAsync(cs => cs.CenterId == centerId && cs.ClassId == classId && cs.StudentId == studentId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AlreadyRemoved_IsIdempotentAndPreservesRemovedAt()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        var oldDate = SeedTimeUtc.AddDays(-5);
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = oldDate, RemovedAt = oldDate, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Removed, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);

        Assert.True(result.IsSuccess);
        var membership = await context.ClassStudents.FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(oldDate, membership.RemovedAt);
    }

    [Fact]
    public async Task AlreadyRemoved_DoesNotSaveOrMutate()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        var oldDate = SeedTimeUtc.AddDays(-5);
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = oldDate, RemovedAt = oldDate, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Removed, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        _interceptor.Reset();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _interceptor.SaveChangesCount);
        var membership = await context.ClassStudents.FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(oldDate, membership.RemovedAt);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    }

    [Fact]
    public async Task UndefinedMembershipStatus_FailsClosed_NoMutation()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc, Status = (EduTwin.Contracts.Organization.ClassStudentStatus)999, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    }

    [Fact]
    public async Task AssignmentTargetHistory_RemainsUnchanged()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });

        var assignmentId = Guid.NewGuid();
        var teacherId = await context.Teachers.Where(t => t.CenterId == centerId).Select(t => t.TeacherId).FirstAsync();
        context.Assignments.Add(new Assignment { AssignmentId = assignmentId, CenterId = centerId, ClassId = classId, CreatedByTeacherId = teacherId, Title = "A1", CreatedAt = SeedTimeUtc, CreatedBy = teacherId, UpdatedAt = SeedTimeUtc, UpdatedBy = teacherId, RowVersion = 1, Status = EduTwin.Contracts.Assignments.AssignmentStatus.Draft });
        var targetOriginalCreatedBy = teacherId;
        context.AssignmentTargets.Add(new AssignmentTarget { CenterId = centerId, AssignmentId = assignmentId, StudentId = studentId, TargetSource = TargetSource.WholeClass, CreatedAt = SeedTimeUtc, CreatedBy = targetOriginalCreatedBy });
        await context.SaveChangesAsync();

        // Precondition assertions
        Assert.True(await context.Teachers.AnyAsync(t => t.TeacherId == teacherId), "Precondition: Teacher must exist.");
        Assert.True(await context.Assignments.AnyAsync(a => a.AssignmentId == assignmentId), "Precondition: Assignment must exist.");
        Assert.True(await context.AssignmentTargets.AnyAsync(at => at.AssignmentId == assignmentId && at.StudentId == studentId), "Precondition: AssignmentTarget must exist.");

        var originalTargetSource = TargetSource.WholeClass;
        var originalTargetCreatedAt = SeedTimeUtc;
        var originalAssignmentCount = await context.Assignments.CountAsync();
        var originalTargetCount = await context.AssignmentTargets.CountAsync();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        await sut.ExecuteAsync(classId, studentId);

        var membership = await context.ClassStudents.FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(EduTwin.Contracts.Organization.ClassStudentStatus.Removed, membership.Status);

        Assert.Equal(originalAssignmentCount, await context.Assignments.CountAsync());
        Assert.Equal(originalTargetCount, await context.AssignmentTargets.CountAsync());

        var target = await context.AssignmentTargets.FirstAsync(at => at.AssignmentId == assignmentId && at.StudentId == studentId);
        Assert.Equal(originalTargetSource, target.TargetSource);
        Assert.Equal(originalTargetCreatedAt, target.CreatedAt);
        Assert.Equal(targetOriginalCreatedBy, target.CreatedBy);
    }

    [Fact]
    public async Task ExactCancellationTokenPassedToOwnershipGuard()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel to prove exact token passed
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), cts.Token)).ThrowsAsync(new OperationCanceledException());

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task ExactCancellationTokenPassedToPersistence()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, studentId);

        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();
        _interceptor.Reset();

        var sut = new RemoveStudentFromClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);

        using var cts = new CancellationTokenSource();
        var exactToken = cts.Token;

        var result = await sut.ExecuteAsync(classId, studentId, exactToken);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, _interceptor.SaveChangesCount);
        Assert.Equal(exactToken, _interceptor.LastToken);
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_ReturnsConcurrencyConflict()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(a => a.CenterId).Returns(centerId);

        var mockContext = new Mock<EduTwinDbContext>(options, tenantAccessorMock.Object) { CallBase = true };

        await SeedBaseDataAsync(mockContext.Object, centerId, classId, studentId);
        mockContext.Object.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await mockContext.Object.SaveChangesAsync();

        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var sut = new RemoveStudentFromClassUseCase(mockContext.Object, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, studentId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task UnrelatedDbUpdateException_IsRethrown()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(a => a.CenterId).Returns(centerId);

        var mockContext = new Mock<EduTwinDbContext>(options, tenantAccessorMock.Object) { CallBase = true };

        await SeedBaseDataAsync(mockContext.Object, centerId, classId, studentId);
        mockContext.Object.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId, JoinedAt = SeedTimeUtc, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, CreatedBy = Guid.NewGuid() });
        await mockContext.Object.SaveChangesAsync();

        mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException());

        var sut = new RemoveStudentFromClassUseCase(mockContext.Object, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(classId, studentId));
    }
}
