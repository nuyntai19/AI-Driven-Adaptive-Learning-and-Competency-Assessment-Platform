using System;
using System.Linq;
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
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class DeleteTeacherUseCaseTests
{
    private readonly Guid _centerId = Guid.Parse("e331c1f3-18d2-43bb-a5a4-1507dfbb7d90");
    private readonly Guid _managerId = Guid.Parse("84f04c63-4402-4fc9-b6eb-bf89bc5f4923");
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2025, 1, 1, 10, 0, 0, TimeSpan.Zero);

    public DeleteTeacherUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_managerId);
        _mockTenantContext.Setup(c => c.Role).Returns("CenterManager");
    }

    private (EduTwinDbContext, ThrowingSaveChangesInterceptor) CreateContext(string dbName)
    {
        var interceptor = new ThrowingSaveChangesInterceptor();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);
        return (new EduTwinDbContext(options, mockAccessor.Object), interceptor);
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

    private async Task SeedDataAsync(EduTwinDbContext context, Guid teacherId, bool isDeleted = false, CenterStatus centerStatus = CenterStatus.Active, bool centerIsDeleted = false, UserRole userRole = UserRole.Teacher, bool userIsDeleted = false, Guid? teacherCenterId = null, Guid? userCenterId = null)
    {
        var actualTeacherCenterId = teacherCenterId ?? _centerId;
        var actualUserCenterId = userCenterId ?? _centerId;

        if (!await context.Centers.AnyAsync(c => c.CenterId == actualTeacherCenterId))
        {
            context.Centers.Add(new Center
            {
                CenterId = actualTeacherCenterId,
                CenterName = "Test Center",
                CenterCode = "TC",
                Timezone = "Asia/Ho_Chi_Minh",
                Status = centerStatus,
                IsDeleted = centerIsDeleted,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            });
        }

        if (actualUserCenterId != actualTeacherCenterId && !await context.Centers.AnyAsync(c => c.CenterId == actualUserCenterId))
        {
            context.Centers.Add(new Center
            {
                CenterId = actualUserCenterId,
                CenterName = "Another Center",
                CenterCode = "AC",
                Timezone = "Asia/Ho_Chi_Minh",
                Status = CenterStatus.Active,
                IsDeleted = false,
                CreatedAt = _fixedTime.UtcDateTime,
                UpdatedAt = _fixedTime.UtcDateTime
            });
        }

        context.Users.Add(new User
        {
            UserId = teacherId,
            CenterId = actualUserCenterId,
            Username = "teacher",
            DisplayName = "Old Name",
            PasswordHash = "hash",
            RoleName = userRole,
            Status = UserStatus.Active,
            AuthVersion = 1,
            IsDeleted = userIsDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        context.Teachers.Add(new Teacher
        {
            TeacherId = teacherId,
            CenterId = actualTeacherCenterId,
            Department = "Old Dept",
            IsDeleted = isDeleted,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public void DependencyInjection_ResolvesDeleteTeacherUseCase()
    {
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("DI").Options;
        services.AddSingleton(new EduTwinDbContext(options));
        services.AddSingleton(_mockTenantContext.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddOrganization();
        var provider = services.BuildServiceProvider();

        var useCase = provider.GetRequiredService<IDeleteTeacherUseCase>();
        Assert.IsType<DeleteTeacherUseCase>(useCase);
    }

    [Fact]
    public async Task DeleteTeacher_Valid_SoftDeletesTeacherAndUser()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);

        var mockTime = new Mock<TimeProvider>();
        var newTime = _fixedTime.AddHours(1);
        mockTime.Setup(t => t.GetUtcNow()).Returns(newTime);

        dbContext.ChangeTracker.Clear();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, mockTime.Object);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);

        dbContext.ChangeTracker.Clear();

        // Verify with new context / no tracking
        Assert.Empty(dbContext.Teachers.Where(t => t.TeacherId == teacherId));
        Assert.Empty(dbContext.Users.Where(u => u.UserId == teacherId));

        var deletedTeacher = await dbContext.Teachers.IgnoreQueryFilters().FirstAsync(t => t.TeacherId == teacherId);
        Assert.True(deletedTeacher.IsDeleted);
        Assert.Equal(newTime.UtcDateTime, deletedTeacher.DeletedAt);
        Assert.Equal(_managerId, deletedTeacher.DeletedBy);
        Assert.Equal(newTime.UtcDateTime, deletedTeacher.UpdatedAt);
        Assert.Equal(_managerId, deletedTeacher.UpdatedBy);

        var deletedUser = await dbContext.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == teacherId);
        Assert.True(deletedUser.IsDeleted);
        Assert.Equal(newTime.UtcDateTime, deletedUser.DeletedAt);
        Assert.Equal(_managerId, deletedUser.DeletedBy);
        Assert.Equal(newTime.UtcDateTime, deletedUser.UpdatedAt);
        Assert.Equal(_managerId, deletedUser.UpdatedBy);
        Assert.Equal(UserStatus.Disabled, deletedUser.Status);
        Assert.Equal(2U, deletedUser.AuthVersion);
        Assert.Equal("teacher", deletedUser.Username);
        Assert.Equal("hash", deletedUser.PasswordHash);
        Assert.Equal(UserRole.Teacher, deletedUser.RoleName);
        Assert.Equal(2UL, deletedUser.RowVersion);
        Assert.Equal(2UL, deletedTeacher.RowVersion);
    }

    [Fact]
    public async Task DeleteTeacher_ActiveClass_InvalidStateTransitionAndNoPersistence()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);

        dbContext.Classes.Add(new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = teacherId,
            ClassName = "C1",
            AcademicYear = "2024-2025",
            Status = ClassStatus.Active,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var mockTime = new Mock<TimeProvider>();
        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, mockTime.Object);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);

        mockTime.Verify(t => t.GetUtcNow(), Times.Never);

        dbContext.ChangeTracker.Clear();
        var teacher = await dbContext.Teachers.FirstAsync(t => t.TeacherId == teacherId);
        Assert.False(teacher.IsDeleted);
        var user = await dbContext.Users.FirstAsync(u => u.UserId == teacherId);
        Assert.False(user.IsDeleted);
    }

    [Fact]
    public async Task DeleteTeacher_ArchivedClass_AllowsSoftDelete()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);

        dbContext.Classes.Add(new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = teacherId,
            ClassName = "C1",
            AcademicYear = "2024-2025",
            Status = ClassStatus.Archived,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteTeacher_SoftDeletedClass_AllowsSoftDelete()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);

        dbContext.Classes.Add(new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = teacherId,
            ClassName = "C1",
            AcademicYear = "2024-2025",
            Status = ClassStatus.Active,
            IsDeleted = true,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.True(result.IsSuccess);
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
    public async Task DeleteTeacher_InvalidTenantOrRole_ResourceNotFound(bool isResolved, string? centerIdStr, string? userIdStr, string? role)
    {
        Guid? centerId = centerIdStr == null ? null : Guid.Parse(centerIdStr);
        Guid? userId = userIdStr == null ? null : Guid.Parse(userIdStr);

        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);
        dbContext.ChangeTracker.Clear();

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(c => c.IsResolved).Returns(isResolved);
        if (centerId.HasValue) mockTenant.Setup(c => c.CenterId).Returns(centerId.Value);
        if (userId.HasValue) mockTenant.Setup(c => c.UserId).Returns(userId.Value);
        mockTenant.Setup(c => c.Role).Returns(role);

        var sut = new DeleteTeacherUseCase(dbContext, mockTenant.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteTeacher_CrossTenant_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, teacherCenterId: otherCenterId, userCenterId: otherCenterId);
        dbContext.ChangeTracker.Clear();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteTeacher_MissingTeacher_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        // Missing

        // Seed an active center to pass the center guard
        dbContext.Centers.Add(new Center
        {
            CenterId = _centerId,
            CenterName = "Test Center",
            CenterCode = "TC",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        });
        await dbContext.SaveChangesAsync();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteTeacher_MissingCenterId_ResourceNotFoundAndNoPersistence()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);
        dbContext.ChangeTracker.Clear();

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(c => c.IsResolved).Returns(true);
        mockTenant.Setup(c => c.CenterId).Returns((Guid?)null);
        mockTenant.Setup(c => c.UserId).Returns(_managerId);
        mockTenant.Setup(c => c.Role).Returns("CenterManager");

        var sut = new DeleteTeacherUseCase(dbContext, mockTenant.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        dbContext.ChangeTracker.Clear();
        var teacher = await dbContext.Teachers.IgnoreQueryFilters().FirstAsync(t => t.TeacherId == teacherId);
        Assert.False(teacher.IsDeleted);
        Assert.Equal(_fixedTime.UtcDateTime, teacher.UpdatedAt);
        Assert.Null(teacher.UpdatedBy);

        var user = await dbContext.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == teacherId);
        Assert.False(user.IsDeleted);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(1U, user.AuthVersion);
        Assert.Equal(_fixedTime.UtcDateTime, user.UpdatedAt);
        Assert.Null(user.UpdatedBy);
    }

    [Fact]
    public async Task DeleteTeacher_MissingUserId_ResourceNotFoundAndNoPersistence()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);
        dbContext.ChangeTracker.Clear();

        var mockTenant = new Mock<ITenantContext>();
        mockTenant.Setup(c => c.IsResolved).Returns(true);
        mockTenant.Setup(c => c.CenterId).Returns(_centerId);
        mockTenant.Setup(c => c.UserId).Returns((Guid?)null);
        mockTenant.Setup(c => c.Role).Returns("CenterManager");

        var sut = new DeleteTeacherUseCase(dbContext, mockTenant.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        dbContext.ChangeTracker.Clear();
        var teacher = await dbContext.Teachers.IgnoreQueryFilters().FirstAsync(t => t.TeacherId == teacherId);
        Assert.False(teacher.IsDeleted);
        Assert.Equal(_fixedTime.UtcDateTime, teacher.UpdatedAt);
        Assert.Null(teacher.UpdatedBy);

        var user = await dbContext.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == teacherId);
        Assert.False(user.IsDeleted);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(1U, user.AuthVersion);
        Assert.Equal(_fixedTime.UtcDateTime, user.UpdatedAt);
        Assert.Null(user.UpdatedBy);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DeleteTeacher_AlreadyDeletedTeacherOrUser_ResourceNotFound(bool teacherDeleted, bool userDeleted)
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, isDeleted: teacherDeleted, userIsDeleted: userDeleted);
        dbContext.ChangeTracker.Clear();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteTeacher_UserWrongRole_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, userRole: UserRole.Student);
        dbContext.ChangeTracker.Clear();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, CenterStatus.Active)]
    [InlineData(false, CenterStatus.Suspended)]
    public async Task DeleteTeacher_CenterDeletedOrSuspended_ResourceNotFound(bool centerIsDeleted, CenterStatus centerStatus)
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId, centerIsDeleted: centerIsDeleted, centerStatus: centerStatus);
        dbContext.ChangeTracker.Clear();

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteTeacher_EmptyTeacherId_ResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, _) = CreateContext(dbName);
        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteTeacher_DbUpdateConcurrencyException_ReturnsConcurrencyConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, interceptor) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);

        var beforeTeacher = await dbContext.Teachers.AsNoTracking().FirstAsync(t => t.TeacherId == teacherId);
        var beforeUser = await dbContext.Users.AsNoTracking().FirstAsync(u => u.UserId == teacherId);

        dbContext.ChangeTracker.Clear();

        interceptor.OnSavingChangesAsyncAction = () => throw new DbUpdateConcurrencyException("Simulated concurrency failure");

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(teacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Empty(dbContext.ChangeTracker.Entries());

        var teacher = await dbContext.Teachers.IgnoreQueryFilters().FirstAsync(t => t.TeacherId == teacherId);
        Assert.False(teacher.IsDeleted);
        Assert.Equal(beforeTeacher.UpdatedAt, teacher.UpdatedAt);
        Assert.Null(teacher.DeletedAt);
        Assert.Null(teacher.DeletedBy);
        Assert.Equal(beforeTeacher.RowVersion, teacher.RowVersion);

        var user = await dbContext.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == teacherId);
        Assert.False(user.IsDeleted);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(1U, user.AuthVersion);
        Assert.Equal(beforeUser.UpdatedAt, user.UpdatedAt);
        Assert.Null(user.DeletedAt);
        Assert.Null(user.DeletedBy);
        Assert.Equal(beforeUser.RowVersion, user.RowVersion);
    }

    [Fact]
    public async Task DeleteTeacher_UnrelatedDbFailure_Rethrows()
    {
        var dbName = Guid.NewGuid().ToString();
        var (dbContext, interceptor) = CreateContext(dbName);
        var teacherId = Guid.NewGuid();
        await SeedDataAsync(dbContext, teacherId);

        var beforeTeacher = await dbContext.Teachers.AsNoTracking().FirstAsync(t => t.TeacherId == teacherId);
        var beforeUser = await dbContext.Users.AsNoTracking().FirstAsync(u => u.UserId == teacherId);

        dbContext.ChangeTracker.Clear();

        interceptor.OnSavingChangesAsyncAction = () => throw new DbUpdateException("Simulated failure");

        var sut = new DeleteTeacherUseCase(dbContext, _mockTenantContext.Object, TimeProvider.System);

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(teacherId));

        dbContext.ChangeTracker.Clear();
        var teacher = await dbContext.Teachers.IgnoreQueryFilters().FirstAsync(t => t.TeacherId == teacherId);
        Assert.False(teacher.IsDeleted);
        Assert.Equal(beforeTeacher.UpdatedAt, teacher.UpdatedAt);
        Assert.Null(teacher.DeletedAt);
        Assert.Null(teacher.DeletedBy);
        Assert.Equal(beforeTeacher.RowVersion, teacher.RowVersion);

        var user = await dbContext.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == teacherId);
        Assert.False(user.IsDeleted);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(1U, user.AuthVersion);
        Assert.Equal(beforeUser.UpdatedAt, user.UpdatedAt);
        Assert.Null(user.DeletedAt);
        Assert.Null(user.DeletedBy);
        Assert.Equal(beforeUser.RowVersion, user.RowVersion);
    }
}
