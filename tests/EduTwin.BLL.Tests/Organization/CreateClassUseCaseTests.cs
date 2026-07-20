using System;
using System.Globalization;
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

public class CreateClassUseCaseTests
{
    private static readonly DateTime SeedTimeUtc = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<CreateClassUseCase>> _mockLogger;
    private readonly Mock<TimeProvider> _mockTimeProvider;

    public CreateClassUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockLogger = new Mock<ILogger<CreateClassUseCase>>();
        _mockTimeProvider = new Mock<TimeProvider>();

        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.UserId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(SeedTimeUtc));
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
        Guid teacherId,
        Guid subjectId,
        bool subjectDeleted = false,
        bool teacherDeleted = false,
        bool userDeleted = false,
        UserRole userRole = UserRole.Teacher,
        Guid? subjectCenterId = null,
        Guid? userCenterId = null)
    {
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterName = "Test Center",
            CenterCode = "TC-1234",
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

        var u = new User
        {
            UserId = teacherId,
            CenterId = userCenterId ?? centerId,
            DisplayName = "John",
            RoleName = userRole,
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

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CenterManager_CreatesClass_Success()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, teacherId, subjectId);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var request = new CreateClassRequest
            {
                ClassName = " Math 101 ",
                AcademicYear = " 2026-2027 ",
                SubjectId = subjectId,
                TeacherId = teacherId
            };
            var result = await sut.ExecuteAsync(request);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Math 101", result.Data.ClassName);
            Assert.Equal("2026-2027", result.Data.AcademicYear);
            Assert.Equal(subjectId.ToString("D").ToLowerInvariant(), result.Data.Subject.SubjectId);
            Assert.Equal("Math", result.Data.Subject.SubjectName);
            Assert.Equal(teacherId.ToString("D").ToLowerInvariant(), result.Data.Teacher.TeacherId);
            Assert.Equal("John", result.Data.Teacher.DisplayName);
            Assert.Equal(0, result.Data.StudentCount);
            Assert.Equal(ClassStatus.Active.ToString(), result.Data.Status);
            Assert.Equal("1", result.Data.RowVersion);
            Assert.NotNull(result.Data.ClassId);

            var dbClass = await context.Classes.FirstOrDefaultAsync(c => c.ClassId == Guid.Parse(result.Data.ClassId));
            Assert.NotNull(dbClass);
            Assert.Equal(SeedTimeUtc, dbClass.CreatedAt);
            Assert.Equal(_mockTenantContext.Object.UserId!.Value, dbClass.CreatedBy);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task ExactCancellationTokenPassed()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, teacherId, subjectId);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest
        {
            ClassName = "Math 101",
            AcademicYear = "2026-2027",
            SubjectId = subjectId,
            TeacherId = teacherId
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(request, cts.Token));
    }

    [Theory]
    [InlineData(false, "c", "u", "CenterManager")]
    [InlineData(true, null, "u", "CenterManager")]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "u", "CenterManager")]
    [InlineData(true, "c", null, "CenterManager")]
    [InlineData(true, "c", "00000000-0000-0000-0000-000000000000", "CenterManager")]
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

        var sut = new CreateClassUseCase(null!, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "a", AcademicYear = "b", SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid() };
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("centermanager")]
    [InlineData("Admin")]
    [InlineData("Student")]
    [InlineData("Teacher")]
    public async Task InvalidRole_ReturnsNotFound(string? role)
    {
        _mockTenantContext.Setup(t => t.Role).Returns(role);

        var sut = new CreateClassUseCase(null!, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "a", AcademicYear = "b", SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid() };
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GuidEmpty_ValidationFailed()
    {
        var sut = new CreateClassUseCase(null!, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "a", AcademicYear = "b", SubjectId = Guid.Empty, TeacherId = Guid.Empty };
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExactRawWhitespace_ValidationFailed_NoPersistence()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);

        // ClassName: 150 chars + " " -> 151 chars
        var request1 = new CreateClassRequest { ClassName = new string('a', 150) + " ", AcademicYear = "2026", SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid() };
        var result1 = await sut.ExecuteAsync(request1);
        Assert.Equal(ErrorCodes.ValidationFailed, result1.ErrorCode);

        // AcademicYear: 20 chars + " " -> 21 chars
        var request2 = new CreateClassRequest { ClassName = "abc", AcademicYear = new string('a', 20) + " ", SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid() };
        var result2 = await sut.ExecuteAsync(request2);
        Assert.Equal(ErrorCodes.ValidationFailed, result2.ErrorCode);

        var count = await context.Classes.CountAsync();
        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData(null, "2026")]
    [InlineData("", "2026")]
    [InlineData("   ", "2026")]
    [InlineData("Math", null)]
    [InlineData("Math", "")]
    [InlineData("Math", "   ")]
    public async Task NullEmptyWhitespace_ValidationFailed_NoPersistence(string? className, string? academicYear)
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest
        {
            ClassName = className!,
            AcademicYear = academicYear!,
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid()
        };

        var result = await sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);

        // Ensure no persistence
        var count = await context.Classes.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SuspendedOrDeletedCenter_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        // 1. Suspended Center test
        var dbNameSuspended = Guid.NewGuid().ToString();
        var contextSuspended = CreateContext(dbNameSuspended, centerId);
        await SeedDataAsync(contextSuspended, centerId, teacherId, subjectId);
        var centerSuspended = await contextSuspended.Centers.FirstAsync();
        centerSuspended.Status = CenterStatus.Suspended;
        await contextSuspended.SaveChangesAsync();

        var sutSuspended = new CreateClassUseCase(contextSuspended, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math", AcademicYear = "2026", SubjectId = subjectId, TeacherId = teacherId };
        var resultSuspended = await sutSuspended.ExecuteAsync(request);

        Assert.False(resultSuspended.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, resultSuspended.ErrorCode);

        // 2. Deleted Center test
        var dbNameDeleted = Guid.NewGuid().ToString();
        var contextDeleted = CreateContext(dbNameDeleted, centerId);
        await SeedDataAsync(contextDeleted, centerId, teacherId, subjectId);
        var centerDeleted = await contextDeleted.Centers.FirstAsync();
        centerDeleted.IsDeleted = true;
        await contextDeleted.SaveChangesAsync();

        var sutDeleted = new CreateClassUseCase(contextDeleted, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var resultDeleted = await sutDeleted.ExecuteAsync(request);

        Assert.False(resultDeleted.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, resultDeleted.ErrorCode);
    }

    [Theory]
    [InlineData(true, false, false, false, false, UserRole.Teacher)]
    [InlineData(false, true, false, false, false, UserRole.Teacher)]
    [InlineData(false, false, true, false, false, UserRole.Teacher)]
    [InlineData(false, false, false, true, false, UserRole.Teacher)]
    [InlineData(false, false, false, false, true, UserRole.Teacher)]
    [InlineData(false, false, false, false, false, UserRole.Student)]
    public async Task RelatedEntitiesCrossTenantOrSoftDeletedOrWrongRole_ReturnsNotFound(
        bool subjectDeleted, bool teacherDeleted, bool userDeleted,
        bool subjectCrossTenant, bool userCrossTenant, UserRole role)
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, teacherId, subjectId,
            subjectDeleted: subjectDeleted,
            teacherDeleted: teacherDeleted,
            userDeleted: userDeleted,
            userRole: role,
            subjectCenterId: subjectCrossTenant ? Guid.NewGuid() : centerId,
            userCenterId: userCrossTenant ? Guid.NewGuid() : centerId);

        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId };
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DuplicateSameTenant_ReturnsDuplicateResource()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, teacherId, subjectId);

        context.Classes.Add(new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = centerId,
            ClassName = "Math 101",
            AcademicYear = "2026-2027",
            Status = ClassStatus.Active,
            SubjectId = subjectId,
            TeacherId = teacherId,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        });
        await context.SaveChangesAsync();

        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = " Math 101 ", AcademicYear = " 2026-2027 ", SubjectId = subjectId, TeacherId = teacherId };
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task SameNameYearDifferentTenant_Allowed()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, teacherId, subjectId);

        context.Classes.Add(new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = Guid.NewGuid(), // Different tenant
            ClassName = "Math 101",
            AcademicYear = "2026-2027",
            Status = ClassStatus.Active,
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        });
        await context.SaveChangesAsync();

        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId };
        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DbUpdateException_DuplicateRaceMapping()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, teacherId, subjectId);

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object, true, "duplicate key value violates unique constraint \"ux_classes_center_id_class_name_academic_year\"");
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Equal(1, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task DbUpdateException_UnrelatedConstraint_Rethrows()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, teacherId, subjectId);

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object, true, "some_other_fk_constraint");
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId };

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(request));
        Assert.Equal(1, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task DuplicateSoftDeleted_DbUpdateException_ReturnsDuplicateResource()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, teacherId, subjectId);

        setupContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, Status = ClassStatus.Active, RowVersion = 1, IsDeleted = true });
        await setupContext.SaveChangesAsync();

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object, true, "ux_classes_center_id_class_name_academic_year");
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Equal(1, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task SuspendedCenter_WithExistingDuplicate_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, teacherId, subjectId);

        var center = await setupContext.Centers.FirstAsync();
        center.Status = CenterStatus.Suspended;

        // Add existing duplicate
        setupContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        await setupContext.SaveChangesAsync();

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object);
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Equal(0, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeletedCenter_WithExistingDuplicate_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, teacherId, subjectId);

        var center = await setupContext.Centers.FirstAsync();
        center.IsDeleted = true;

        // Add existing duplicate
        setupContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        await setupContext.SaveChangesAsync();

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object);
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Equal(0, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task InvalidSubject_WithExistingDuplicate_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, teacherId, subjectId);

        // Add existing duplicate
        setupContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        await setupContext.SaveChangesAsync();

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object);
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        // Invalid SubjectId
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = Guid.NewGuid(), TeacherId = teacherId };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Equal(0, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task InvalidTeacher_WithExistingDuplicate_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, teacherId, subjectId);

        // Add existing duplicate
        setupContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = teacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        await setupContext.SaveChangesAsync();

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object);
        var sut = new CreateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        // Invalid TeacherId
        var request = new CreateClassRequest { ClassName = "Math 101", AcademicYear = "2026-2027", SubjectId = subjectId, TeacherId = Guid.NewGuid() };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Equal(0, context.SaveChangesCallCount);
    }
}

public class TestRaceConditionDbContext : EduTwinDbContext
{
    private readonly bool _shouldThrow;
    private readonly string _exceptionMessage;
    public int SaveChangesCallCount { get; private set; }

    public TestRaceConditionDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantIdAccessor, bool shouldThrow = false, string exceptionMessage = "")
        : base(options, tenantIdAccessor)
    {
        _shouldThrow = shouldThrow;
        _exceptionMessage = exceptionMessage;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        if (_shouldThrow)
        {
            var inner = new Exception(_exceptionMessage);
            throw new DbUpdateException("An error occurred while saving the entity changes.", inner);
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
