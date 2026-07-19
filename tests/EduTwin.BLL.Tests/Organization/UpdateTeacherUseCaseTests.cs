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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class UpdateTeacherUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Guid _centerId = Guid.Parse("e331c1f3-18d2-43bb-a5a4-1507dfbb7d90");
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public UpdateTeacherUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_managerId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));
    }

    private class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public Action? OnSavingChangesAsyncAction { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            OnSavingChangesAsyncAction?.Invoke();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private (EduTwinDbContext, ThrowingSaveChangesInterceptor) CreateContext(string dbName)
    {
        var interceptor = new ThrowingSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);
        return (new EduTwinDbContext(options, mockAccessor.Object), interceptor);
    }

    private async Task SeedDataAsync(EduTwinDbContext context, Guid teacherId, ulong rowVersion = 1, bool isDeleted = false, CenterStatus centerStatus = CenterStatus.Active, bool centerIsDeleted = false, UserRole userRole = UserRole.Teacher, bool userIsDeleted = false, Guid? teacherCenterId = null, Guid? userCenterId = null)
    {
        var actualTeacherCenterId = teacherCenterId ?? _centerId;
        var actualUserCenterId = userCenterId ?? _centerId;

        if (!context.Centers.Any(c => c.CenterId == _centerId))
        {
            context.Centers.Add(new Center
            {
                CenterId = _centerId,
                CenterName = "Test",
                CenterCode = "TEST",
                Timezone = "Asia/Ho_Chi_Minh",
                Status = centerStatus,
                IsDeleted = centerIsDeleted,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            });
        }

        if (actualTeacherCenterId != _centerId && !context.Centers.Any(c => c.CenterId == actualTeacherCenterId))
        {
            context.Centers.Add(new Center
            {
                CenterId = actualTeacherCenterId,
                CenterName = "Other",
                CenterCode = "OTH",
                Timezone = "UTC",
                Status = CenterStatus.Active,
                IsDeleted = false,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            });
        }

        var user = new User
        {
            UserId = teacherId,
            CenterId = actualUserCenterId,
            Username = "teacher",
            RoleName = userRole,
            DisplayName = "Old Name",
            Status = UserStatus.Active,
            AuthVersion = 1,
            IsDeleted = userIsDeleted,
            PasswordHash = "hash",
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        context.Users.Add(user);

        if (userRole == UserRole.Teacher)
        {
            var teacher = new Teacher
            {
                TeacherId = teacherId,
                CenterId = actualTeacherCenterId,
                Department = "Old Dept",
                RowVersion = rowVersion,
                IsDeleted = isDeleted,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            };
            context.Teachers.Add(teacher);
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public void OrganizationDependencyInjection_ResolvesUpdateTeacherUseCase()
    {
        var services = new ServiceCollection();
        services.AddOrganization();
        var mockContext = new Mock<EduTwinDbContext>(new DbContextOptionsBuilder<EduTwinDbContext>().Options, Mock.Of<ITenantIdAccessor>());
        services.AddSingleton(mockContext.Object);
        services.AddSingleton(_mockTenantContext.Object);
        services.AddSingleton(TimeProvider.System);

        var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredService<IUpdateTeacherUseCase>();
        Assert.NotNull(resolved);
        Assert.IsType<UpdateTeacherUseCase>(resolved);
    }

    [Fact]
    public async Task UpdateTeacher_ValidRequest_Success()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, rowVersion: 5);

        // Seed an active class to test ClassCount
        dbContext.Classes.Add(new Class
        {
            ClassId = Guid.NewGuid(), CenterId = _centerId, TeacherId = teacherId, ClassName = "C1", AcademicYear = "2024-2025", Status = ClassStatus.Active, IsDeleted = false, CreatedAt = _fixedTime.UtcDateTime, UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();
        var seededTeacher = await dbContext.Teachers.FirstAsync(t => t.TeacherId == teacherId);
        var actualRowVersion = seededTeacher.RowVersion;
        dbContext.ChangeTracker.Clear();

        var mockTime = new Mock<TimeProvider>();
        var newTime = _fixedTime.AddHours(1);
        mockTime.Setup(t => t.GetUtcNow()).Returns(newTime);

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, mockTime.Object);
        var request = new UpdateTeacherRequest
        {
            DisplayName = "New Name",
            Department = "New Dept",
            Status = UserStatus.Locked,
            RowVersion = actualRowVersion.ToString()
        };

        var result = await sut.ExecuteAsync(teacherId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(teacherId.ToString("D").ToLowerInvariant(), result.Data.TeacherId);
        Assert.Equal("teacher", result.Data.Username); // username unchanged
        Assert.Equal("New Name", result.Data.DisplayName);
        Assert.Equal("New Dept", result.Data.Department);
        Assert.Equal(UserStatus.Locked.ToString(), result.Data.Status);
        Assert.Equal(1, result.Data.ClassCount);
        Assert.Equal((actualRowVersion + 1).ToString(), result.Data.RowVersion); // old + 1

        var updatedTeacher = await dbContext.Teachers.Include(t => t.User).FirstAsync(t => t.TeacherId == teacherId);
        Assert.Equal(actualRowVersion + 1, updatedTeacher.RowVersion);
        Assert.Equal(newTime.UtcDateTime, updatedTeacher.UpdatedAt);
        Assert.Equal(_managerId, updatedTeacher.UpdatedBy);
        Assert.Equal(newTime.UtcDateTime, updatedTeacher.User!.UpdatedAt);
        Assert.Equal(_managerId, updatedTeacher.User.UpdatedBy);
        Assert.Equal("teacher", updatedTeacher.User.Username); // unchanged
        Assert.Equal(1U, updatedTeacher.User.AuthVersion); // unchanged
        Assert.Equal("hash", updatedTeacher.User.PasswordHash); // unchanged
    }

    [Theory]
    [InlineData("   ")] // whitespace
    [InlineData("+1")] // explicit plus
    [InlineData("-1")] // explicit minus
    [InlineData("1.5")] // decimal
    [InlineData("١٢٣")] // Unicode digits
    [InlineData("0")] // 0
    [InlineData("18446744073709551616")] // overflow ulong
    [InlineData("abc")] // invalid format
    public async Task UpdateTeacher_InvalidRowVersion_ValidationFailed(string rowVersion)
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);
        dbContext.ChangeTracker.Clear();

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest
        {
            DisplayName = "N",
            Status = UserStatus.Active,
            RowVersion = rowVersion
        };

        var result = await sut.ExecuteAsync(teacherId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateTeacher_StaleRowVersion_ConcurrencyConflictAndNoPersistence()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, rowVersion: 10);

        var seededTeacher = await dbContext.Teachers.FirstAsync(t => t.TeacherId == teacherId);
        var actualRowVersion = seededTeacher.RowVersion;
        dbContext.ChangeTracker.Clear();

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest
        {
            DisplayName = "N",
            Status = UserStatus.Active,
            RowVersion = (actualRowVersion + 1).ToString() // mismatched
        };

        var result = await sut.ExecuteAsync(teacherId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);

        var teacher = await dbContext.Teachers.AsNoTracking().FirstAsync(t => t.TeacherId == teacherId);
        Assert.Equal(actualRowVersion, teacher.RowVersion); // Not modified
    }

    [Fact]
    public async Task UpdateTeacher_DbUpdateConcurrencyException_ReturnsConcurrencyConflictAndRollbacks()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, interceptor) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, rowVersion: 5);

        var seededTeacher = await dbContext.Teachers.FirstAsync(t => t.TeacherId == teacherId);
        var actualRowVersion = seededTeacher.RowVersion;
        dbContext.ChangeTracker.Clear();

        interceptor.OnSavingChangesAsyncAction = () => throw new DbUpdateConcurrencyException("Simulated concurrency failure");

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest
        {
            DisplayName = "New",
            Status = UserStatus.Active,
            RowVersion = actualRowVersion.ToString()
        };

        var result = await sut.ExecuteAsync(teacherId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task UpdateTeacher_UnrelatedSaveFailure_Rethrows()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, interceptor) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, rowVersion: 5);

        var seededTeacher = await dbContext.Teachers.FirstAsync(t => t.TeacherId == teacherId);
        var actualRowVersion = seededTeacher.RowVersion;
        dbContext.ChangeTracker.Clear();

        interceptor.OnSavingChangesAsyncAction = () => throw new DbUpdateException("Simulated unrelated failure");

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest
        {
            DisplayName = "New",
            Status = UserStatus.Active,
            RowVersion = actualRowVersion.ToString()
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(teacherId, request));
    }

    [Theory]
    [InlineData(false, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")] // Not resolved
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")] // Empty center
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "00000000-0000-0000-0000-000000000000", "CenterManager")] // Empty user
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Teacher")] // Wrong role
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Student")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Admin")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "centermanager")] // Case sensitive
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", " ")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", null)]
    public async Task UpdateTeacher_InvalidTenantOrRole_ResourceNotFound(bool isResolved, string? centerIdStr, string? userIdStr, string? role)
    {
        Guid? centerId = centerIdStr == null ? null : Guid.Parse(centerIdStr);
        Guid? userId = userIdStr == null ? null : Guid.Parse(userIdStr);
        _mockTenantContext.Setup(c => c.IsResolved).Returns(isResolved);
        _mockTenantContext.Setup(c => c.CenterId).Returns(centerId ?? Guid.Empty);
        _mockTenantContext.Setup(c => c.UserId).Returns(userId ?? Guid.Empty);
        _mockTenantContext.Setup(c => c.Role).Returns(role!);

        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest { DisplayName = "N", Status = UserStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(teacherId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, CenterStatus.Active)] // deleted center
    [InlineData(false, CenterStatus.Suspended)] // suspended center
    public async Task UpdateTeacher_CenterDeletedOrSuspended_ResourceNotFound(bool isDeleted, CenterStatus status)
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();

        var badCenterId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.CenterId).Returns(badCenterId);

        dbContext.Centers.Add(new Center
        {
            CenterId = badCenterId, CenterName = "Bad", CenterCode = "BAD", Timezone = "Asia/Ho_Chi_Minh", Status = status, IsDeleted = isDeleted, CreatedAt = _fixedTime.UtcDateTime, UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest { DisplayName = "N", Status = UserStatus.Active, RowVersion = "1" };
        var result = await sut.ExecuteAsync(teacherId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateTeacher_GuidEmpty_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest { DisplayName = "N", Status = UserStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(Guid.Empty, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", false, UserRole.Teacher)] // Teacher soft deleted
    [InlineData(false, "00000000-0000-0000-0000-000000000001", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", false, UserRole.Teacher)] // Teacher wrong center
    [InlineData(false, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "00000000-0000-0000-0000-000000000001", false, UserRole.Teacher)] // User wrong center
    [InlineData(false, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", true, UserRole.Teacher)] // User soft deleted
    [InlineData(false, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", false, UserRole.Student)] // User wrong role
    public async Task UpdateTeacher_TeacherOrUserInvalid_ResourceNotFound(bool teacherDeleted, string teacherCenter, string userCenter, bool userDeleted, UserRole role)
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, isDeleted: teacherDeleted, teacherCenterId: Guid.Parse(teacherCenter), userCenterId: Guid.Parse(userCenter), userIsDeleted: userDeleted, userRole: role);

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest { DisplayName = "N", Status = UserStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(teacherId, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateTeacher_MissingTeacher_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        // Only seed center, no teacher
        dbContext.Centers.Add(new Center
        {
            CenterId = _centerId, CenterName = "Test", CenterCode = "TEST", Timezone = "UTC", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = _fixedTime.UtcDateTime, UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest { DisplayName = "N", Status = UserStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(Guid.NewGuid(), request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateTeacher_DepartmentWhitespace_SetsToNull()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, rowVersion: 1);
        dbContext.ChangeTracker.Clear();

        var sut = new UpdateTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);
        var request = new UpdateTeacherRequest { DisplayName = "N", Department = "   ", Status = UserStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(teacherId, request);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Data!.Department);
    }
}
