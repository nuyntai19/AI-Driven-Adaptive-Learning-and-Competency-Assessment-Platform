using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
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

public class GetTeacherUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ITeacherOwnershipGuard> _mockGuard;
    private readonly GetTeacherUseCase _sut;
    private readonly Guid _centerId = Guid.NewGuid();

    public GetTeacherUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockGuard = new Mock<ITeacherOwnershipGuard>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        // Default guard setup to allowed
        _mockGuard.Setup(g => g.CheckTeacherAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(OwnershipDecision.Allowed);

        _sut = new GetTeacherUseCase(_dbContext, _mockTenantContext.Object, _mockGuard.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private async Task<Teacher> SeedTeacherAsync(Guid? centerId = null, Guid? teacherId = null, string username = "teacher", string displayName = "Teacher", string department = "Math", UserStatus status = UserStatus.Active, bool softDeletedTeacher = false, bool softDeletedUser = false, UserRole roleName = UserRole.Teacher)
    {
        var targetCenterId = centerId ?? _centerId;
        var targetTeacherId = teacherId ?? Guid.NewGuid();

        var user = new User
        {
            UserId = targetTeacherId,
            CenterId = targetCenterId,
            Username = username,
            DisplayName = displayName,
            Status = status,
            RoleName = roleName,
            IsDeleted = softDeletedUser,
            PasswordHash = "hashed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var teacher = new Teacher
        {
            TeacherId = targetTeacherId,
            CenterId = targetCenterId,
            Department = department,
            IsDeleted = softDeletedTeacher,
            User = user,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Teachers.Add(teacher);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return teacher;
    }

    private async Task SeedClassAsync(Guid teacherId, ClassStatus status, bool isDeleted = false)
    {
        var c = new Class
        {
            ClassId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = teacherId,
            SubjectId = Guid.NewGuid(),
            ClassName = "Class",
            AcademicYear = "2024",
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Classes.Add(c);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetTeacher_CenterManagerSameTenant_Success()
    {
        var teacher = await SeedTeacherAsync();
        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(teacher.TeacherId.ToString("D").ToLowerInvariant(), result.Data.TeacherId);
    }

    [Fact]
    public async Task GetTeacher_TeacherViewsSelf_Success()
    {
        // Guard is mock, so role doesn't matter much for BLL test if guard says Allowed
        var teacher = await SeedTeacherAsync();
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));
        _mockTenantContext.Setup(c => c.UserId).Returns(teacher.TeacherId);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(teacher.TeacherId.ToString("D").ToLowerInvariant(), result.Data.TeacherId);
    }

    [Fact]
    public async Task GetTeacher_TeacherViewsOther_Forbidden()
    {
        var teacher = await SeedTeacherAsync();
        _mockGuard.Setup(g => g.CheckTeacherAccessAsync(teacher.TeacherId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(OwnershipDecision.Forbidden);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_StudentDecision_Forbidden()
    {
        var teacher = await SeedTeacherAsync();
        _mockGuard.Setup(g => g.CheckTeacherAccessAsync(teacher.TeacherId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(OwnershipDecision.Forbidden);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_OwnershipNotFound_ResourceNotFound()
    {
        var teacher = await SeedTeacherAsync();
        _mockGuard.Setup(g => g.CheckTeacherAccessAsync(teacher.TeacherId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(OwnershipDecision.NotFound);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_UndefinedOwnershipDecision_FailsClosed()
    {
        var teacher = await SeedTeacherAsync();
        _mockGuard.Setup(g => g.CheckTeacherAccessAsync(teacher.TeacherId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((OwnershipDecision)999);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetTeacher_CrossTenant_ResourceNotFound()
    {
        var crossTenantId = Guid.NewGuid();
        var teacher = await SeedTeacherAsync(centerId: crossTenantId);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_SoftDeletedTeacher_ResourceNotFound()
    {
        var teacher = await SeedTeacherAsync(softDeletedTeacher: true);
        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_SoftDeletedUser_ResourceNotFound()
    {
        var teacher = await SeedTeacherAsync(softDeletedUser: true);
        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_UserNotTeacherRole_ResourceNotFound()
    {
        var teacher = await SeedTeacherAsync(roleName: UserRole.Student);
        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_GuidEmpty_ResourceNotFoundAndGuardNotCalled()
    {
        var result = await _sut.ExecuteAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        _mockGuard.Verify(g => g.CheckTeacherAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, "e331c1f3-18d2-43bb-a5a4-1507dfbb7d90")] // Unresolved
    [InlineData(true, null)] // Missing
    [InlineData(true, "00000000-0000-0000-0000-000000000000")] // Empty
    public async Task GetTeacher_TenantContextInvalid_ResourceNotFound(bool isResolved, string? centerIdStr)
    {
        Guid? centerId = centerIdStr == null ? null : Guid.Parse(centerIdStr);
        _mockTenantContext.Setup(c => c.IsResolved).Returns(isResolved);
        _mockTenantContext.Setup(c => c.CenterId).Returns(centerId);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetTeacher_DtoMappedExactly()
    {
        var teacher = await SeedTeacherAsync(username: "john.doe", displayName: "John Doe", department: "Science", status: UserStatus.Locked);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.True(result.IsSuccess);
        var dto = result.Data!;
        Assert.Equal(teacher.TeacherId.ToString("D").ToLowerInvariant(), dto.TeacherId);
        Assert.Equal("john.doe", dto.Username);
        Assert.Equal("John Doe", dto.DisplayName);
        Assert.Equal("Science", dto.Department);
        Assert.Equal(nameof(UserStatus.Locked), dto.Status);
        Assert.Equal(2, dto.ClassCount);
        Assert.Equal(teacher.RowVersion.ToString(CultureInfo.InvariantCulture), dto.RowVersion);
    }

    [Fact]
    public async Task GetTeacher_CountsOnlyActiveClasses()
    {
        var teacher = await SeedTeacherAsync();
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Active, isDeleted: true);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Archived);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ClassCount);
    }

    [Fact]
    public async Task GetTeacher_DoesNotCountArchivedClasses()
    {
        var teacher = await SeedTeacherAsync();
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Archived);
        await SeedClassAsync(teacher.TeacherId, ClassStatus.Archived, isDeleted: true);

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.ClassCount);
    }

    [Fact]
    public async Task GetTeacher_QueryDoesNotTrackEntities()
    {
        var teacher = await SeedTeacherAsync();

        var result = await _sut.ExecuteAsync(teacher.TeacherId);

        Assert.True(result.IsSuccess);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetTeacher_EntityDisappearsAfterAllowed_ResourceNotFound()
    {
        // Guard returns Allowed, but we don't seed the DB
        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public void OrganizationDependencyInjection_ResolvesGetTeacherUseCase()
    {
        var services = new ServiceCollection();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("DI").Options;
        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        services.AddSingleton(new EduTwinDbContext(options, mockAccessor.Object));
        services.AddSingleton(new Mock<ITenantContext>().Object);
        services.AddSingleton(new Mock<ITeacherOwnershipGuard>().Object);
        services.AddSingleton<TimeProvider>(Mock.Of<TimeProvider>());

        services.AddOrganization();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IGetTeacherUseCase>();
        Assert.NotNull(resolved);
        Assert.IsType<GetTeacherUseCase>(resolved);
    }
}
