using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.Platform;

public class PlatformTenantIsolationTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Guid _platformCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId;
    private readonly Guid _customerCenterId = Guid.NewGuid();

    public PlatformTenantIsolationTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        // Seed Root Tenant PLATFORM and a customer tenant
        _dbContext.Centers.AddRange(
            new Center
            {
                CenterId = _platformCenterId,
                CenterCode = "PLATFORM",
                CenterName = "EduTwin Platform Administration",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Center
            {
                CenterId = _customerCenterId,
                CenterCode = "CUSTOMER_A",
                CenterName = "Customer Center A",
                Status = CenterStatus.Active,
                Timezone = "Asia/Ho_Chi_Minh",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        // Seed users in each tenant
        _dbContext.Users.AddRange(
            new User
            {
                UserId = Guid.NewGuid(),
                CenterId = _platformCenterId,
                Username = "platform.admin",
                DisplayName = "Platform Admin",
                RoleName = UserRole.PlatformAdmin,
                Status = UserStatus.Active,
                AuthVersion = 1,
                PasswordHash = "hashed_pass",
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                UserId = Guid.NewGuid(),
                CenterId = _customerCenterId,
                Username = "customer.manager",
                DisplayName = "Customer Manager",
                RoleName = UserRole.CenterManager,
                Status = UserStatus.Active,
                AuthVersion = 1,
                PasswordHash = "hashed_pass",
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CustomerTenantContext_CannotQueryPlatformTenantUsers()
    {
        // Set tenant context to Customer Center A
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_customerCenterId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.CenterManager));

        var visibleUsers = await _dbContext.Users.ToListAsync();

        Assert.Single(visibleUsers);
        Assert.Equal("customer.manager", visibleUsers[0].Username);
        Assert.DoesNotContain(visibleUsers, u => u.RoleName == UserRole.PlatformAdmin);
    }

    [Fact]
    public async Task PlatformTenantContext_CannotDirectlyQueryCustomerUsersWithoutIgnoreFilters()
    {
        // Set tenant context to Root Tenant PLATFORM
        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_platformCenterId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.PlatformAdmin));

        var visibleUsers = await _dbContext.Users.ToListAsync();

        Assert.Single(visibleUsers);
        Assert.Equal("platform.admin", visibleUsers[0].Username);
        Assert.DoesNotContain(visibleUsers, u => u.RoleName == UserRole.CenterManager);
    }
}
