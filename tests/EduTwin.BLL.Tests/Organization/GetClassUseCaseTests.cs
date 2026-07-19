using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.Organization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class GetClassUseCaseTests
{
    private static readonly DateTime SeedTimeUtc =
        new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<GetClassUseCase>> _mockLogger;
    private readonly Mock<IClassOwnershipGuard> _mockOwnershipGuard;

    public GetClassUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockLogger = new Mock<ILogger<GetClassUseCase>>();
        _mockOwnershipGuard = new Mock<IClassOwnershipGuard>();

        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.UserId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));
    }

    private EduTwinDbContext CreateContext(string dbName, Guid centerId)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    private async Task SeedDataAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid classId,
        Guid teacherId,
        Guid subjectId,
        bool isDeleted = false,
        bool subjectDeleted = false,
        bool teacherDeleted = false,
        bool userDeleted = false,
        Guid? subjectCenterId = null,
        Guid? userCenterId = null)
    {
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterName = "Test Center",
            CenterCode = "TC-" + centerId.ToString().Substring(0, 4),
            Timezone = "UTC",
            Status = CenterStatus.Active,
            RowVersion = 1,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        });

        context.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = subjectCenterId ?? centerId,
            SubjectName = "Math",
            SubjectCode = "MATH",
            IsDeleted = subjectDeleted,
            RowVersion = 1,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        });

        // FK: Teacher(CenterId, TeacherId) → User(CenterId, UserId)
        // teacherId must equal UserId for the relationship to resolve.
        var u = new User
        {
            UserId = teacherId,
            CenterId = userCenterId ?? centerId,
            DisplayName = "John",
            RoleName = UserRole.Teacher,
            IsDeleted = userDeleted,
            Username = "test_" + teacherId,
            PasswordHash = "hash",
            Status = UserStatus.Active,
            RowVersion = 1,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        };
        context.Users.Add(u);

        context.Teachers.Add(new Teacher
        {
            TeacherId = teacherId,
            CenterId = userCenterId ?? centerId,
            User = u,
            IsDeleted = teacherDeleted,
            RowVersion = 1,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        });

        context.Classes.Add(new Class
        {
            ClassId = classId,
            CenterId = centerId,
            ClassName = "Class 1",
            AcademicYear = "2026-2027",
            Status = ClassStatus.Active,
            SubjectId = subjectId,
            TeacherId = teacherId,
            IsDeleted = isDeleted,
            RowVersion = 1,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        });

        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, Status = ClassStudentStatus.Active, StudentId = Guid.NewGuid(), JoinedAt = SeedTimeUtc });
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, Status = ClassStudentStatus.Removed, StudentId = Guid.NewGuid(), JoinedAt = SeedTimeUtc, RemovedAt = SeedTimeUtc });
        context.ClassStudents.Add(new ClassStudent { CenterId = Guid.NewGuid(), ClassId = classId, Status = ClassStudentStatus.Active, StudentId = Guid.NewGuid(), JoinedAt = SeedTimeUtc });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CenterManager_GetsSameTenantClass()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var classId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, teacherId, subjectId);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var sut = new GetClassUseCase(context, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(classId.ToString("D").ToLowerInvariant(), result.Data.ClassId);
            Assert.Equal(subjectId.ToString("D").ToLowerInvariant(), result.Data.Subject.SubjectId);
            Assert.Equal(teacherId.ToString("D").ToLowerInvariant(), result.Data.Teacher.TeacherId);
            Assert.Equal("1", result.Data.RowVersion); // Invariant
            Assert.Equal(1, result.Data.StudentCount); // Only active same-tenant
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task TeacherOwner_GetsClass()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var classId = Guid.NewGuid();

        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));
        await SeedDataAsync(context, centerId, classId, Guid.NewGuid(), Guid.NewGuid());
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetClassUseCase(context, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task OtherTeacher_ReturnsForbidden()
    {
        var classId = Guid.NewGuid();
        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Forbidden);

        var sut = new GetClassUseCase(null!, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenant_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid(); // different tenant
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var classId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, Guid.NewGuid(), Guid.NewGuid());
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetClassUseCase(context, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedClass_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var classId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, Guid.NewGuid(), Guid.NewGuid(), isDeleted: true);
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetClassUseCase(context, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GuidEmpty_ReturnsNotFound()
    {
        var sut = new GetClassUseCase(null!, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, "c", "u", "r")] // Unresolved
    [InlineData(true, null, "u", "r")] // Null CenterId
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "u", "r")] // Empty CenterId
    [InlineData(true, "c", null, "r")] // Null UserId
    [InlineData(true, "c", "00000000-0000-0000-0000-000000000000", "r")] // Empty UserId
    public async Task InvalidTenantContext_ReturnsNotFound(bool isResolved, string? centerIdStr, string? userIdStr, string? role)
    {
        _mockTenantContext.Setup(t => t.IsResolved).Returns(isResolved);

        if (centerIdStr == "c") _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        else if (centerIdStr != null) _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.Parse(centerIdStr));
        else _mockTenantContext.Setup(t => t.CenterId).Returns((Guid?)null);

        if (userIdStr == "u") _mockTenantContext.Setup(t => t.UserId).Returns(Guid.NewGuid());
        else if (userIdStr != null) _mockTenantContext.Setup(t => t.UserId).Returns(Guid.Parse(userIdStr));
        else _mockTenantContext.Setup(t => t.UserId).Returns((Guid?)null);

        _mockTenantContext.Setup(t => t.Role).Returns(role);

        var sut = new GetClassUseCase(null!, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("teacher")]
    [InlineData("centerManager")]
    [InlineData("Admin")]
    [InlineData("Student")]
    public async Task InvalidRole_ReturnsNotFound_GuardNotCalled(string? role)
    {
        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.UserId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.Role).Returns(role);

        var sut = new GetClassUseCase(null!, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UndefinedOwnershipDecision_ReturnsNotFound()
    {
        var classId = Guid.NewGuid();
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync((OwnershipDecision)999);

        var sut = new GetClassUseCase(null!, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task NotFoundOwnershipDecision_ReturnsNotFound()
    {
        var classId = Guid.NewGuid();
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.NotFound);

        var sut = new GetClassUseCase(null!, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public async Task RelatedEntitiesCrossTenantOrSoftDeleted_ReturnsNotFound(
        bool subjectDeleted, bool teacherDeleted, bool userDeleted,
        bool subjectCrossTenant, bool userCrossTenant)
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var classId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, Guid.NewGuid(), Guid.NewGuid(),
            subjectDeleted: subjectDeleted,
            teacherDeleted: teacherDeleted,
            userDeleted: userDeleted,
            subjectCenterId: subjectCrossTenant ? Guid.NewGuid() : centerId,
            userCenterId: userCrossTenant ? Guid.NewGuid() : centerId);

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetClassUseCase(context, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExactCancellationTokenPassed()
    {
        var classId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, cts.Token)).ReturnsAsync(OwnershipDecision.Forbidden).Verifiable();

        var sut = new GetClassUseCase(null!, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        await sut.ExecuteAsync(classId, cts.Token);

        _mockOwnershipGuard.Verify();
    }

    [Fact]
    public async Task ReadQuery_DoesNotTrackEntities()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var classId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, Guid.NewGuid(), Guid.NewGuid());
        context.ChangeTracker.Clear();

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnershipDecision.Allowed);

        var sut = new GetClassUseCase(context, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        var result = await sut.ExecuteAsync(classId);

        Assert.True(result.IsSuccess);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task AllowedOwnership_CanceledToken_CancelsEfQuery()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var classId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, Guid.NewGuid(), Guid.NewGuid());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockOwnershipGuard.Setup(g => g.CheckClassAccessAsync(classId, cts.Token)).ReturnsAsync(OwnershipDecision.Allowed).Verifiable();

        var sut = new GetClassUseCase(context, _mockTenantContext.Object, _mockLogger.Object, _mockOwnershipGuard.Object);
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(classId, cts.Token));

        _mockOwnershipGuard.Verify(g => g.CheckClassAccessAsync(classId, cts.Token), Times.Once);
    }
}
