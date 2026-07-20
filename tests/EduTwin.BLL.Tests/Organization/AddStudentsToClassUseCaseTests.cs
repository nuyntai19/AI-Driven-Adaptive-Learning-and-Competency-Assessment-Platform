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
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.Organization;

public class AddStudentsToClassUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Mock<IClassOwnershipGuard> _mockOwnershipGuard;
    private readonly Mock<ILogger<AddStudentsToClassUseCase>> _mockLogger;
    private static readonly DateTime SeedTimeUtc = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);

    public AddStudentsToClassUseCaseTests()
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

        _mockLogger = new Mock<ILogger<AddStudentsToClassUseCase>>();
    }

    private static EduTwinDbContext CreateContext(string dbName, Guid centerId)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(a => a.CenterId).Returns(centerId);

        return new EduTwinDbContext(options, tenantAccessorMock.Object);
    }

    private async Task SeedBaseDataAsync(EduTwinDbContext context, Guid centerId, Guid classId, List<Guid> studentIds)
    {
        context.Centers.Add(new Center { CenterId = centerId, CenterName = "Center", CenterCode = "C1", Timezone = "UTC", Status = CenterStatus.Active, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        var teacherId = Guid.NewGuid();
        var teacherUser = new User { UserId = teacherId, CenterId = centerId, DisplayName = "Teacher", RoleName = UserRole.Teacher, Username = "T1", PasswordHash = "H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc };
        context.Users.Add(teacherUser);
        context.Teachers.Add(new Teacher { TeacherId = teacherId, CenterId = centerId, User = teacherUser, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        var subjectId = Guid.NewGuid();
        context.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectName = "Subject", SubjectCode = "S1", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        context.Classes.Add(new Class { ClassId = classId, CenterId = centerId, ClassName = "Class 1", AcademicYear = "2026", SubjectId = subjectId, TeacherId = teacherId, CreatedAt = SeedTimeUtc, CreatedBy = Guid.NewGuid(), UpdatedAt = SeedTimeUtc, UpdatedBy = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = 1 });

        foreach (var sId in studentIds)
        {
            var user = new User { UserId = sId, CenterId = centerId, DisplayName = "Student " + sId, RoleName = UserRole.Student, Username = "S_" + sId, PasswordHash = "H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc };
            context.Users.Add(user);
            context.Students.Add(new Student { StudentId = sId, CenterId = centerId, GradeLevel = 10, FullName = "Student " + sId, User = user, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task EmptyClassId_ReturnsValidationFailed()
    {
        var sut = new AddStudentsToClassUseCase(CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid()), _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.Empty, new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, true, true, true, true, "CenterManager")]
    [InlineData(true, false, true, true, true, "CenterManager")]
    [InlineData(true, true, false, true, true, "CenterManager")]
    [InlineData(true, true, true, false, true, "CenterManager")]
    [InlineData(true, true, true, true, false, "CenterManager")]
    [InlineData(true, true, true, true, true, "Student")]
    [InlineData(true, true, true, true, true, "")]
    [InlineData(true, true, true, true, true, null)]
    public async Task InvalidTenantOrRole_ReturnsResourceNotFound(bool resolved, bool centerIdHasValue, bool centerIdNotEmpty, bool userIdHasValue, bool userIdNotEmpty, string? role)
    {
        var tenantMock = new Mock<ITenantContext>();
        tenantMock.Setup(t => t.IsResolved).Returns(resolved);
        tenantMock.Setup(t => t.CenterId).Returns(centerIdHasValue ? (centerIdNotEmpty ? Guid.NewGuid() : Guid.Empty) : null);
        tenantMock.Setup(t => t.UserId).Returns(userIdHasValue ? (userIdNotEmpty ? Guid.NewGuid() : Guid.Empty) : null);
        tenantMock.Setup(t => t.Role).Returns(role);

        var sut = new AddStudentsToClassUseCase(CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid()), tenantMock.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(OwnershipDecision.Forbidden, ErrorCodes.ForbiddenResource)]
    [InlineData(OwnershipDecision.NotFound, ErrorCodes.ResourceNotFound)]
    [InlineData((OwnershipDecision)999, ErrorCodes.ResourceNotFound)]
    public async Task OwnershipGuard_ReturnsCorrectErrorCode(OwnershipDecision decision, string expectedErrorCode)
    {
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(decision);

        var sut = new AddStudentsToClassUseCase(CreateContext(Guid.NewGuid().ToString(), _mockTenantContext.Object.CenterId!.Value), _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } });

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task ClassNotFoundOrDeleted_ReturnsResourceNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);

        await SeedBaseDataAsync(context, centerId, classId, new List<Guid>());
        var clazz = await context.Classes.FirstAsync();
        clazz.IsDeleted = true;
        await context.SaveChangesAsync();

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CenterDeletedOrSuspended_ReturnsResourceNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);

        await SeedBaseDataAsync(context, centerId, classId, new List<Guid>());
        var center = await context.Centers.FirstAsync();
        center.Status = CenterStatus.Suspended;
        await context.SaveChangesAsync();

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MixedValidAndInvalidStudents_ReturnsResourceNotFound_WithNoPersistence()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();

        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, new List<Guid> { s1 }); // S2 not seeded

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, new AddStudentsToClassRequest { StudentIds = new[] { s1, s2 } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var membershipCount = await context.ClassStudents.CountAsync();
        Assert.Equal(0, membershipCount);
    }

    [Fact]
    public async Task NewActiveAndRemovedStudents_HandledCorrectly_AndIsIdempotent()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var s1New = Guid.NewGuid();
        var s2Active = Guid.NewGuid();
        var s3Removed = Guid.NewGuid();

        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, new List<Guid> { s1New, s2Active, s3Removed });

        var oldDate = SeedTimeUtc.AddDays(-1);
        var originalCreatedBy = Guid.NewGuid();
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = s2Active, JoinedAt = oldDate, Status = ClassStudentStatus.Active, CreatedBy = originalCreatedBy });
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = s3Removed, JoinedAt = oldDate, RemovedAt = oldDate, Status = ClassStudentStatus.Removed, CreatedBy = originalCreatedBy });
        await context.SaveChangesAsync();

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var request = new AddStudentsToClassRequest { StudentIds = new[] { s1New, s2Active, s3Removed } };

        var result = await sut.ExecuteAsync(classId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.AddedCount);
        Assert.Equal(1, result.Data.AlreadyMemberCount);

        var ms1 = await context.ClassStudents.FirstAsync(cs => cs.StudentId == s1New);
        Assert.Equal(ClassStudentStatus.Active, ms1.Status);
        Assert.Equal(SeedTimeUtc, ms1.JoinedAt);
        Assert.Null(ms1.RemovedAt);
        Assert.Equal(_mockTenantContext.Object.UserId!.Value, ms1.CreatedBy);

        var ms2 = await context.ClassStudents.FirstAsync(cs => cs.StudentId == s2Active);
        Assert.Equal(ClassStudentStatus.Active, ms2.Status);
        Assert.Equal(oldDate, ms2.JoinedAt);
        Assert.Equal(originalCreatedBy, ms2.CreatedBy);

        var ms3 = await context.ClassStudents.FirstAsync(cs => cs.StudentId == s3Removed);
        Assert.Equal(ClassStudentStatus.Active, ms3.Status);
        Assert.Equal(SeedTimeUtc, ms3.JoinedAt);
        Assert.Null(ms3.RemovedAt);
        Assert.Equal(_mockTenantContext.Object.UserId!.Value, ms3.CreatedBy);

        // Test idempotency
        var resultIdempotent = await sut.ExecuteAsync(classId, request);
        Assert.True(resultIdempotent.IsSuccess);
        Assert.Equal(0, resultIdempotent.Data!.AddedCount);
        Assert.Equal(3, resultIdempotent.Data.AlreadyMemberCount);
        Assert.Equal(3, await context.ClassStudents.CountAsync());
    }

    [Fact]
    public async Task CrossTenantStudent_ReturnsResourceNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var context = CreateContext(Guid.NewGuid().ToString(), centerId);
        await SeedBaseDataAsync(context, centerId, classId, new List<Guid>());

        // Seed cross-tenant student
        var otherCenterId = Guid.NewGuid();
        var user = new User { UserId = studentId, CenterId = otherCenterId, DisplayName = "S", RoleName = UserRole.Student, Username = "S", PasswordHash = "H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc };
        context.Users.Add(user);
        context.Students.Add(new Student { StudentId = studentId, CenterId = otherCenterId, GradeLevel = 10, FullName = "S", User = user, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        await context.SaveChangesAsync();

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, new AddStudentsToClassRequest { StudentIds = new[] { studentId } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Request_NullRequest_ReturnsValidationFailed_NoDatabaseCall()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), null!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        Assert.Equal(0, await context.ClassStudents.CountAsync());
    }

    [Fact]
    public async Task Request_NullStudentIds_ReturnsValidationFailed_NoDatabaseCall()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), new AddStudentsToClassRequest { StudentIds = null! });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        Assert.Equal(0, await context.ClassStudents.CountAsync());
    }

    [Fact]
    public async Task Request_EmptyStudentIds_ReturnsValidationFailed_NoDatabaseCall()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), new AddStudentsToClassRequest { StudentIds = Array.Empty<Guid>() });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        Assert.Equal(0, await context.ClassStudents.CountAsync());
    }

    [Fact]
    public async Task Request_ContainsEmptyGuid_ReturnsValidationFailed_NoDatabaseCall()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid(), Guid.Empty } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        Assert.Equal(0, await context.ClassStudents.CountAsync());
    }

    [Fact]
    public async Task Request_ContainsDuplicateIds_ReturnsValidationFailed_NoDatabaseCall()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        var id = Guid.NewGuid();
        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), new AddStudentsToClassRequest { StudentIds = new[] { id, id } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
        Assert.Equal(0, await context.ClassStudents.CountAsync());
    }

    [Fact]
    public async Task InvalidUserStatus_ReturnsResourceNotFound_NoMutation()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);

        await SeedBaseDataAsync(context, centerId, classId, new List<Guid> { studentId });
        var user = await context.Users.FirstAsync(u => u.UserId == studentId);
        user.Status = (UserStatus)999;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, new AddStudentsToClassRequest { StudentIds = new[] { studentId } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    }

    [Fact]
    public async Task InvalidClassStudentStatus_ReturnsResourceNotFound_NoMutation()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId1 = Guid.NewGuid();
        var studentId2 = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);

        await SeedBaseDataAsync(context, centerId, classId, new List<Guid> { studentId1, studentId2 });
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = studentId1, JoinedAt = SeedTimeUtc, Status = (ClassStudentStatus)999, CreatedBy = Guid.NewGuid() });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, new AddStudentsToClassRequest { StudentIds = new[] { studentId1, studentId2 } });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted);
    }

    [Fact]
    public async Task CancellationToken_PassedToOwnershipGuard()
    {
        var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid());
        var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel to prove it throws if passed

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), cts.Token))
                           .ThrowsAsync(new OperationCanceledException());

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(Guid.NewGuid(), new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } }, cts.Token));
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.Disabled)]
    public async Task ValidDefinedUserStatuses_AreAccepted(UserStatus status)
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var context = CreateContext(Guid.NewGuid().ToString(), centerId);

        await SeedBaseDataAsync(context, centerId, classId, new List<Guid> { studentId });
        var user = await context.Users.FirstAsync(u => u.UserId == studentId);
        user.Status = status;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var sut = new AddStudentsToClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockOwnershipGuard.Object, _mockLogger.Object);
        var result = await sut.ExecuteAsync(classId, new AddStudentsToClassRequest { StudentIds = new[] { studentId } });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.AddedCount);
        var membership = await context.ClassStudents.FirstAsync(cs => cs.StudentId == studentId);
        Assert.Equal(ClassStudentStatus.Active, membership.Status);
    }
}
