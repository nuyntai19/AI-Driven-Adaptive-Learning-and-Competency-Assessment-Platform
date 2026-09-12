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
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.Tests.Platform;

public class PlatformPrivilegeEscalationTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IAuthorizationSnapshotReader> _mockSnapshotReader;
    private readonly Mock<ITenantAdministratorGuard> _mockAdminGuard;
    private readonly Guid _customerCenterId = Guid.NewGuid();
    private readonly Guid _managerUserId = Guid.NewGuid();
    private static readonly DateTime FixedUtcNow = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
    private readonly Mock<TimeProvider> _mockTimeProvider;

    public PlatformPrivilegeEscalationTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockSnapshotReader = new Mock<IAuthorizationSnapshotReader>();
        _mockAdminGuard = new Mock<ITenantAdministratorGuard>();
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider
            .Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_customerCenterId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_managerUserId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        _dbContext.Centers.Add(new Center
        {
            CenterId = _customerCenterId,
            CenterCode = "CENTER_TEST",
            CenterName = "Test Center",
            Status = Contracts.Organization.CenterStatus.Active,
            Timezone = "UTC",
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        _dbContext.Users.Add(new User
        {
            UserId = _managerUserId,
            CenterId = _customerCenterId,
            Username = "customer.manager",
            DisplayName = "Customer Manager",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            AuthVersion = 1,
            PasswordHash = "hashed_pass",
            RowVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateAuthorizationRole_WhenAccountTypeIsPlatformAdmin_ReturnsAuthPrivilegeEscalation()
    {
        var sut = new CreateAuthorizationRoleUseCase(_dbContext, _mockTenantContext.Object, _mockTimeProvider.Object);

        var request = new CreateAuthorizationRoleRequest
        {
            RoleCode = "CUSTOM_PLATFORM_ADMIN",
            RoleName = "Custom Platform Admin",
            AccountType = UserRole.PlatformAdmin,
            Description = "Attempting to create platform admin role"
        };

        var result = await sut.ExecuteAsync(request, "trace-test-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthPrivilegeEscalation, result.ErrorCode);
    }

    [Fact]
    public async Task ReplaceUserRoles_WhenAssigningPlatformAdminRole_ReturnsAuthPrivilegeEscalation()
    {
        var sut = new ReplaceUserRolesUseCase(
            _dbContext,
            _mockTenantContext.Object,
            _mockSnapshotReader.Object,
            _mockAdminGuard.Object,
            _mockTimeProvider.Object);

        var platformRole = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = _customerCenterId,
            RoleCode = "FAKE_PLATFORM_ROLE",
            RoleName = "Fake Platform Role",
            AccountType = UserRole.PlatformAdmin,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        };
        _dbContext.AuthorizationRoles.Add(platformRole);
        await _dbContext.SaveChangesAsync();

        var request = new ReplaceUserRolesRequest
        {
            RoleIds = [platformRole.RoleId],
            RowVersion = "1",
            Reason = "Attempting to assign platform role"
        };

        var result = await sut.ExecuteAsync(_managerUserId, request, "trace-test-2");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthPrivilegeEscalation, result.ErrorCode);
    }

    [Fact]
    public async Task ReplaceRolePermissions_WhenGrantingPlatformPermission_ReturnsAuthPrivilegeEscalation()
    {
        var sut = new ReplaceRolePermissionsUseCase(
            _dbContext,
            _mockTenantContext.Object,
            _mockSnapshotReader.Object,
            _mockAdminGuard.Object,
            _mockTimeProvider.Object);

        var tenantRole = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = _customerCenterId,
            RoleCode = "TENANT_ROLE",
            RoleName = "Tenant Role",
            AccountType = UserRole.CenterManager,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow,
            RowVersion = 1
        };
        _dbContext.AuthorizationRoles.Add(tenantRole);
        await _dbContext.SaveChangesAsync();

        var request = new ReplaceRolePermissionsRequest
        {
            PermissionCodes = ["platform.centers.manage"],
            RowVersion = "1",
            Reason = "Attempting to grant platform permission"
        };

        var result = await sut.ExecuteAsync(tenantRole.RoleId, request, "trace-test-3");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthPrivilegeEscalation, result.ErrorCode);
    }
}
