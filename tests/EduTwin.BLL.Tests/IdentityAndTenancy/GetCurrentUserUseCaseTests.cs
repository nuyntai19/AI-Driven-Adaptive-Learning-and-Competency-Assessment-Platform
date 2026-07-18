using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class GetCurrentUserUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly GetCurrentUserUseCase _sut;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GetCurrentUserUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_userId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        _sut = new GetCurrentUserUseCase(_dbContext, _mockTenantContext.Object);

        _dbContext.Centers.Add(new Center { CenterId = _centerId, CenterCode = "C1", CenterName = "C1", Status = CenterStatus.Active, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private User CreateUser(Guid id, Guid centerId, UserRole role, UserStatus status = UserStatus.Active, bool isDeleted = false)
    {
        return new User
        {
            UserId = id,
            CenterId = centerId,
            Username = "user1",
            DisplayName = "User One",
            RoleName = role,
            Status = status,
            PasswordHash = "hash",
            AuthVersion = 1,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetCurrentUser_CenterManagerActive_Success()
    {
        _dbContext.Users.Add(CreateUser(_userId, _centerId, UserRole.CenterManager));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(_userId.ToString("D").ToLowerInvariant(), result.Data.UserId);
        Assert.Equal(_centerId.ToString("D").ToLowerInvariant(), result.Data.CenterId);
        Assert.Equal("C1", result.Data.CenterName);
        Assert.Equal("user1", result.Data.Username);
        Assert.Equal("User One", result.Data.DisplayName);
        Assert.Equal("CenterManager", result.Data.Role);
        Assert.Equal("Active", result.Data.Status);
    }

    [Fact]
    public async Task GetCurrentUser_TeacherActive_Success()
    {
        _dbContext.Users.Add(CreateUser(_userId, _centerId, UserRole.Teacher));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Teacher", result.Data!.Role);
    }

    [Fact]
    public async Task GetCurrentUser_StudentActive_Success()
    {
        _dbContext.Users.Add(CreateUser(_userId, _centerId, UserRole.Student));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Student", result.Data!.Role);
    }

    [Fact]
    public async Task GetCurrentUser_CrossTenant_ResourceNotFound()
    {
        var otherCenterId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, CenterCode = "C2", CenterName = "C2", Status = CenterStatus.Active, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Users.Add(CreateUser(_userId, otherCenterId, UserRole.Student));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_MissingUser_ResourceNotFound()
    {
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_SoftDeletedUser_ResourceNotFound()
    {
        _dbContext.Users.Add(CreateUser(_userId, _centerId, UserRole.Student, isDeleted: true));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_MissingCenter_ResourceNotFound()
    {
        var otherCenterId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.CenterId).Returns(otherCenterId);
        _dbContext.Users.Add(CreateUser(_userId, otherCenterId, UserRole.Student));
        await _dbContext.SaveChangesAsync();
        // C2 is not in db

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_SoftDeletedCenter_ResourceNotFound()
    {
        var otherCenterId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.CenterId).Returns(otherCenterId);
        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, CenterCode = "C2", CenterName = "C2", Status = CenterStatus.Active, Timezone = "UTC", IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Users.Add(CreateUser(_userId, otherCenterId, UserRole.Student));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_SuspendedCenter_AuthUserDisabled()
    {
        var otherCenterId = Guid.NewGuid();
        _mockTenantContext.Setup(c => c.CenterId).Returns(otherCenterId);
        _dbContext.Centers.Add(new Center { CenterId = otherCenterId, CenterCode = "C2", CenterName = "C2", Status = CenterStatus.Suspended, Timezone = "UTC", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Users.Add(CreateUser(_userId, otherCenterId, UserRole.Student));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_LockedUser_AuthUserDisabled()
    {
        _dbContext.Users.Add(CreateUser(_userId, _centerId, UserRole.Student, status: UserStatus.Locked));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_DisabledUser_AuthUserDisabled()
    {
        _dbContext.Users.Add(CreateUser(_userId, _centerId, UserRole.Student, status: UserStatus.Disabled));
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_UnresolvedTenantContext_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_MissingCenterId_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.CenterId).Returns((Guid?)null);
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_MissingUserId_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.UserId).Returns((Guid?)null);
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task GetCurrentUser_MissingRole_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns((string?)null);
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetCurrentUser_EmptyRole_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns(string.Empty);
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetCurrentUser_WhitespaceRole_ResourceNotFound()
    {
        _mockTenantContext.Setup(c => c.Role).Returns("   ");
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("teacher")]
    [InlineData("centermanager")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    public async Task GetCurrentUser_InvalidRole_ResourceNotFound(string invalidRole)
    {
        _mockTenantContext.Setup(c => c.Role).Returns(invalidRole);
        var result = await _sut.ExecuteAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task DependencyInjection_ShouldResolveIGetCurrentUserUseCase()
    {
        var services = new ServiceCollection();
        services.AddScoped(sp => _dbContext);
        services.AddScoped(sp => _mockTenantContext.Object);
        services.AddScoped<ITenantContextInitializer>(sp => new Mock<ITenantContextInitializer>().Object);
        services.AddScoped<IBackgroundTenantScopeFactory>(sp => new Mock<IBackgroundTenantScopeFactory>().Object);
        services.AddScoped<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>(sp => new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>().Object);

        services.AddIdentityAndTenancy();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var useCase = scope.ServiceProvider.GetRequiredService<IGetCurrentUserUseCase>();

        Assert.NotNull(useCase);
        Assert.IsType<GetCurrentUserUseCase>(useCase);
    }
}
