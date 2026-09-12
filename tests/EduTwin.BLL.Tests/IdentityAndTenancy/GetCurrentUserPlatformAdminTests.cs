using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class GetCurrentUserPlatformAdminTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IAuthorizationSnapshotReader> _mockAuthorizationSnapshotReader;
    private readonly GetCurrentUserUseCase _sut;
    private readonly Guid _platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
    private readonly Guid _adminUserId = Guid.NewGuid();

    public GetCurrentUserPlatformAdminTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockAuthorizationSnapshotReader = new Mock<IAuthorizationSnapshotReader>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_platformCenterId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_adminUserId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.PlatformAdmin));

        _mockAuthorizationSnapshotReader
            .Setup(reader => reader.ReadForUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationSnapshot(
                [new AuthorizationRoleSummaryDto { RoleId = Guid.NewGuid().ToString("D"), RoleCode = "PLATFORM_ADMIN", RoleName = "Quản trị viên nền tảng", AccountType = nameof(UserRole.PlatformAdmin) }],
                ["platform.centers.read", "platform.centers.manage", "platform.managers.manage"]));

        _sut = new GetCurrentUserUseCase(
            _dbContext,
            _mockTenantContext.Object,
            _mockAuthorizationSnapshotReader.Object);

        _dbContext.Centers.Add(new Center
        {
            CenterId = _platformCenterId,
            CenterCode = "PLATFORM",
            CenterName = "EduTwin Platform Administration",
            Status = CenterStatus.Active,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_PlatformAdmin_ReturnsSuccessWithPlatformAdminDetails()
    {
        // Arrange
        var adminUser = new User
        {
            UserId = _adminUserId,
            CenterId = _platformCenterId,
            Username = "platform.admin",
            DisplayName = "Platform Administrator",
            RoleName = UserRole.PlatformAdmin,
            Status = UserStatus.Active,
            AuthVersion = 1,
            PasswordHash = "hashed_pass",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        _dbContext.Users.Add(adminUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("platform.admin", result.Data.Username);
        Assert.Equal(nameof(UserRole.PlatformAdmin), result.Data.AccountType);
        Assert.Equal(nameof(UserRole.PlatformAdmin), result.Data.Role);
        Assert.Contains("platform.centers.read", result.Data.Permissions);
        Assert.Contains("platform.centers.manage", result.Data.Permissions);
    }

    [Fact]
    public async Task ExecuteAsync_PlatformAdmin_WhenCenterSuspended_ReturnsAuthUserDisabled()
    {
        // Arrange
        var platformCenter = await _dbContext.Centers.FindAsync(_platformCenterId);
        platformCenter!.Status = CenterStatus.Suspended;

        var adminUser = new User
        {
            UserId = _adminUserId,
            CenterId = _platformCenterId,
            Username = "platform.admin",
            DisplayName = "Platform Administrator",
            RoleName = UserRole.PlatformAdmin,
            Status = UserStatus.Active,
            AuthVersion = 1,
            PasswordHash = "hashed_pass",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        _dbContext.Users.Add(adminUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.ExecuteAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthUserDisabled, result.ErrorCode);
    }
}
