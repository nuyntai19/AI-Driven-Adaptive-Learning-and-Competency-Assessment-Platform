using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.Organization;

public class CreateTeacherUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IPasswordHasher<User>> _mockHasher;
    private readonly CreateTeacherUseCase _sut;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public CreateTeacherUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockHasher = new Mock<IPasswordHasher<User>>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_managerId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        var mockTime = new Mock<TimeProvider>();
        mockTime.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
                   .Returns("hashed_password");

        _sut = new CreateTeacherUseCase(_dbContext, _mockTenantContext.Object, mockTime.Object, _mockHasher.Object);

        SeedCenter();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SeedCenter(bool isDeleted = false, CenterStatus status = CenterStatus.Active)
    {
        _dbContext.Centers.Add(new Center
        {
            CenterId = _centerId,
            CenterName = "Test Center",
            CenterCode = "TEST-01",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task CreateTeacher_ValidRequest_Success()
    {
        var request = new CreateTeacherRequest
        {
            Username = " teacher.new ",
            DisplayName = " New Teacher ",
            Department = " Math ",
            TemporaryPassword = " Password123! " // space intentionally included
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        var dto = result.Data;
        Assert.Equal("teacher.new", dto.Username);
        Assert.Equal("New Teacher", dto.DisplayName);
        Assert.Equal("Math", dto.Department);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(0, dto.ClassCount);
        Assert.Equal("1", dto.RowVersion);

        var teacherId = Guid.Parse(dto.TeacherId);
        var userInDb = await _dbContext.Users.FindAsync(teacherId);
        var teacherInDb = await _dbContext.Teachers.FindAsync(teacherId);

        Assert.NotNull(userInDb);
        Assert.NotNull(teacherInDb);
        Assert.Equal(teacherId, userInDb.UserId);
        Assert.Equal(teacherId, teacherInDb.TeacherId);
        Assert.Equal(_centerId, userInDb.CenterId);
        Assert.Equal(_centerId, teacherInDb.CenterId);

        Assert.Equal("teacher.new", userInDb.Username);
        Assert.Equal("New Teacher", userInDb.DisplayName);
        Assert.Equal(UserRole.Teacher, userInDb.RoleName);
        Assert.Equal(UserStatus.Active, userInDb.Status);
        Assert.Equal(1u, userInDb.AuthVersion);
        Assert.Null(userInDb.LastLoginAt);
        Assert.False(userInDb.IsDeleted);
        Assert.Equal(_fixedTime.UtcDateTime, userInDb.CreatedAt);
        Assert.Equal(_managerId, userInDb.CreatedBy);
        Assert.Equal("hashed_password", userInDb.PasswordHash);

        Assert.Equal("Math", teacherInDb.Department);
        Assert.Null(teacherInDb.Bio);
        Assert.False(teacherInDb.IsDeleted);
        Assert.Equal(_fixedTime.UtcDateTime, teacherInDb.CreatedAt);
        Assert.Equal(_managerId, teacherInDb.CreatedBy);

        _mockHasher.Verify(h => h.HashPassword(It.IsAny<User>(), " Password123! "), Times.Once);
    }

    [Fact]
    public async Task CreateTeacher_DuplicateUsernameSameTenant_DuplicateResource()
    {
        _dbContext.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            CenterId = _centerId,
            Username = "teacher.dup",
            DisplayName = "Existing",
            Status = UserStatus.Active,
            RoleName = UserRole.Teacher,
            IsDeleted = false,
            PasswordHash = "hash",
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await _dbContext.SaveChangesAsync();

        var request = new CreateTeacherRequest
        {
            Username = "teacher.dup",
            DisplayName = "New",
            TemporaryPassword = "Password123!!"
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);

        _mockHasher.Verify(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateTeacher_DuplicateUsernameCrossTenant_Success()
    {
        var otherCenter = Guid.NewGuid();
        _dbContext.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            CenterId = otherCenter,
            Username = "teacher.dup",
            DisplayName = "Existing",
            Status = UserStatus.Active,
            RoleName = UserRole.Teacher,
            IsDeleted = false,
            PasswordHash = "hash",
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await _dbContext.SaveChangesAsync();

        var request = new CreateTeacherRequest
        {
            Username = "teacher.dup",
            DisplayName = "New",
            TemporaryPassword = "Password123!!"
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("", "ValidPassword123!")] // empty username
    [InlineData("user", "short")] // short password
    [InlineData(null, "ValidPassword123!")] // null username
    [InlineData("a_very_long_username_that_exceeds_the_one_hundred_characters_limit_allowed_for_username_field_123456789", "ValidPassword123!")] // username > 100
    [InlineData("user", "a_very_long_password_that_exceeds_the_two_hundred_characters_limit_allowed_for_password_field_which_is_really_long_123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890")] // password > 200
    public async Task CreateTeacher_InvalidRequest_ValidationFailed(string? username, string? pwd)
    {
        var request = new CreateTeacherRequest
        {
            Username = username!,
            DisplayName = "Name",
            TemporaryPassword = pwd!
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockHasher.Verify(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(false, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")]
    [InlineData(true, null, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "CenterManager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", null, "CenterManager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "00000000-0000-0000-0000-000000000000", "CenterManager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Teacher")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Student")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "Admin")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "centermanager")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", null)]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", " ")]
    [InlineData(true, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90", "123")]
    public async Task CreateTeacher_InvalidTenantOrRole_ResourceNotFound(bool isResolved, string? centerIdStr, string? userIdStr, string? role)
    {
        Guid? centerId = centerIdStr == null ? null : Guid.Parse(centerIdStr);
        Guid? userId = userIdStr == null ? null : Guid.Parse(userIdStr);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(isResolved);
        _mockTenantContext.Setup(c => c.CenterId).Returns(centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(userId);
        _mockTenantContext.Setup(c => c.Role).Returns(role);

        var request = new CreateTeacherRequest { Username = "u", TemporaryPassword = "password123!", DisplayName = "n" };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, CenterStatus.Active)]
    [InlineData(false, CenterStatus.Suspended)]
    public async Task CreateTeacher_CenterDeletedOrSuspended_ResourceNotFound(bool isDeleted, CenterStatus status)
    {
        var badCenterId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.CenterId).Returns(badCenterId);
        _dbContext.Centers.Add(new Center
        {
            CenterId = badCenterId,
            CenterName = "Bad",
            CenterCode = "BAD-01",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await _dbContext.SaveChangesAsync();

        var request = new CreateTeacherRequest { Username = "u", TemporaryPassword = "password123!", DisplayName = "n" };
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CreateTeacher_HashPasswordThrows_NoPersistence()
    {
        _mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
                   .Throws(new InvalidOperationException("Hasher failed"));

        var request = new CreateTeacherRequest { Username = "u", TemporaryPassword = "password123!", DisplayName = "n" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExecuteAsync(request));

        Assert.Empty(_dbContext.Users.Where(u => u.Username == "u"));
        Assert.Empty(_dbContext.Teachers.Where(t => t.User!.Username == "u"));
    }

    [Fact]
    public async Task CreateTeacher_WhitespaceOnlyPassword_ValidationFailed()
    {
        var request = new CreateTeacherRequest { Username = "u", TemporaryPassword = "            ", DisplayName = "n" };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        _mockHasher.Verify(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        Assert.Empty(_dbContext.Users.Where(u => u.Username == "u"));
    }

    [Fact]
    public async Task CreateTeacher_MissingCenter_ResourceNotFound()
    {
        var request = new CreateTeacherRequest { Username = "u", TemporaryPassword = "password123!", DisplayName = "n" };
        var badCenterId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.CenterId).Returns(badCenterId);
        // Do not seed this center

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    private class ThrowingSaveChangesInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public Action? OnSavingChangesAsyncAction { get; set; }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            OnSavingChangesAsyncAction?.Invoke();
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private EduTwinDbContext CreateContextWithInterceptor(ThrowingSaveChangesInterceptor interceptor, string dbName)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;
        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    [Fact]
    public async Task CreateTeacher_UnrelatedSaveFailure_RethrowsAndPersistsNothing()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockTime = new Mock<TimeProvider>();
        mockTime.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var interceptor = new ThrowingSaveChangesInterceptor();
        using var dbContext = CreateContextWithInterceptor(interceptor, dbName);
        dbContext.Centers.Add(new Center
        {
            CenterId = _centerId, CenterName = "Test", CenterCode = "TEST", Timezone = "Asia/Ho_Chi_Minh", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = _fixedTime.UtcDateTime, UpdatedAt = _fixedTime.UtcDateTime
        });
        dbContext.SaveChanges();

        interceptor.OnSavingChangesAsyncAction = () => throw new DbUpdateException("Simulated failure");

        var sut = new CreateTeacherUseCase(dbContext, _mockTenantContext.Object, mockTime.Object, _mockHasher.Object);
        var request = new CreateTeacherRequest { Username = "u", TemporaryPassword = "password123!", DisplayName = "n" };

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(request));

        dbContext.ChangeTracker.Clear();
        Assert.Empty(dbContext.Users.Where(u => u.Username == "u"));
        Assert.Empty(dbContext.Teachers);
    }

    [Fact]
    public async Task CreateTeacher_ConcurrentDuplicateFailure_ReturnsDuplicateResource()
    {
        var dbName = Guid.NewGuid().ToString();
        var mockTime = new Mock<TimeProvider>();
        mockTime.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        var interceptor = new ThrowingSaveChangesInterceptor();
        using var dbContext = CreateContextWithInterceptor(interceptor, dbName);

        dbContext.Centers.Add(new Center
        {
            CenterId = _centerId, CenterName = "Test", CenterCode = "TEST", Timezone = "Asia/Ho_Chi_Minh", Status = CenterStatus.Active, IsDeleted = false, CreatedAt = _fixedTime.UtcDateTime, UpdatedAt = _fixedTime.UtcDateTime
        });
        dbContext.SaveChanges();

        interceptor.OnSavingChangesAsyncAction = () =>
        {
            // Simulate another thread/request inserting the same username right before we save
            var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
            var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
            mockAccessor.Setup(a => a.CenterId).Returns(_centerId);
            using var separateContext = new EduTwinDbContext(options, mockAccessor.Object);

            if (!separateContext.Users.Any(u => u.Username == "u"))
            {
                separateContext.Users.Add(new User
                {
                    UserId = Guid.NewGuid(), CenterId = _centerId, Username = "u", RoleName = UserRole.Teacher, DisplayName = "n", Status = UserStatus.Active, AuthVersion = 1, IsDeleted = false, PasswordHash = "hash", CreatedAt = _fixedTime.UtcDateTime, UpdatedAt = _fixedTime.UtcDateTime
                });
                separateContext.SaveChanges();
            }
            throw new DbUpdateException("Duplicate constraint simulated");
        };

        var sut = new CreateTeacherUseCase(dbContext, _mockTenantContext.Object, mockTime.Object, _mockHasher.Object);
        var request = new CreateTeacherRequest { Username = "u", TemporaryPassword = "password123!", DisplayName = "n" };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);

        dbContext.ChangeTracker.Clear();
        // Only 1 user should exist, not 2
        Assert.Single(dbContext.Users.Where(u => u.Username == "u"));
        Assert.Empty(dbContext.Teachers);
    }

    [Fact]
    public void OrganizationDependencyInjection_ResolvesCreateTeacherUseCase()
    {
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("DI").Options;
        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        services.AddSingleton(new EduTwinDbContext(options, mockAccessor.Object));
        services.AddSingleton(new Mock<ITenantContext>().Object);
        services.AddSingleton<TimeProvider>(Mock.Of<TimeProvider>());
        services.AddSingleton(new Mock<IPasswordHasher<User>>().Object);

        services.AddOrganization();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<ICreateTeacherUseCase>();
        Assert.NotNull(resolved);
        Assert.IsType<CreateTeacherUseCase>(resolved);
    }
}
