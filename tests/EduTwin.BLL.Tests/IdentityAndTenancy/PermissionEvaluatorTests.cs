using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class PermissionEvaluatorTests
{
    [Fact]
    public async Task HasPermissionAsync_UsesActiveDynamicGrantWithoutLegacyRoleShortcut()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.Initialize(
            centerId,
            managerId,
            nameof(UserRole.CenterManager),
            1);
        await using var context = CreateContext(tenantContext);
        await SeedCenterManagerAsync(context, centerId, managerId);
        await new AuthorizationBootstrapper(
            context,
            new FixedTimeProvider(DateTimeOffset.UtcNow)).EnsureAsync();
        var sut = new PermissionEvaluator(context, tenantContext);

        Assert.True(await sut.HasPermissionAsync("authorization.roles.read"));
        Assert.False(await sut.HasPermissionAsync("learning.attempts.submit"));
        Assert.False(await sut.HasPermissionAsync(" "));

        var assignment = await context.UserRoleAssignments.SingleAsync();
        assignment.Status = UserRoleAssignmentStatus.Revoked;
        assignment.RevokedAt = DateTime.UtcNow;
        assignment.RevokedByUserId = managerId;
        assignment.RevokeReason = "Regression test";
        await context.SaveChangesAsync();

        Assert.False(await sut.HasPermissionAsync("authorization.roles.read"));
    }

    [Fact]
    public async Task HasPermissionAsync_CrossTenantUserId_FailsClosed()
    {
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        var managerA = Guid.NewGuid();
        var managerB = Guid.NewGuid();
        var accessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessor.SetupGet(item => item.CenterId).Returns(centerA);
        await using var context = CreateContext(accessor.Object);
        await SeedCenterManagerAsync(context, centerA, managerA);
        await SeedCenterManagerAsync(context, centerB, managerB, includeCatalog: false);
        await new AuthorizationBootstrapper(
            context,
            new FixedTimeProvider(DateTimeOffset.UtcNow)).EnsureAsync();
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(item => item.IsResolved).Returns(true);
        tenantContext.SetupGet(item => item.CenterId).Returns(centerA);
        tenantContext.SetupGet(item => item.UserId).Returns(managerB);
        var sut = new PermissionEvaluator(context, tenantContext.Object);

        Assert.False(await sut.HasPermissionAsync("authorization.roles.read"));
    }

    private static EduTwinDbContext CreateContext(
        EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor accessor)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EduTwinDbContext(options, accessor);
    }

    private static async Task SeedCenterManagerAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid managerId,
        bool includeCatalog = true)
    {
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = $"C-{centerId:N}"[..20],
            CenterName = "Permission evaluator center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Bangkok",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Users.Add(new User
        {
            UserId = managerId,
            CenterId = centerId,
            Username = $"manager-{managerId:N}",
            PasswordHash = "not-used",
            RoleName = UserRole.CenterManager,
            DisplayName = "Manager",
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        if (includeCatalog)
        {
            context.Permissions.AddRange(
                AuthorizationPermissionCatalog.CreatePermissions());
            context.PermissionAccountTypes.AddRange(
                AuthorizationPermissionCatalog.CreateAccountTypeMappings());
        }

        await context.SaveChangesAsync();
    }
}
