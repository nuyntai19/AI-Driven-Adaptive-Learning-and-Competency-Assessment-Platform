using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class AuthorizationSnapshotReaderTests
{
    private static readonly DateTimeOffset UtcNow =
        new(2026, 9, 9, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReadForUserAsync_ReturnsOnlyCurrentTenantActiveAuthorization()
    {
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        var managerA = Guid.NewGuid();
        var managerB = Guid.NewGuid();
        var tenantContext = new TenantContext();
        await using var context = CreateContext(tenantContext);
        await SeedBootstrapDataAsync(
            context,
            (centerA, managerA, "A"),
            (centerB, managerB, "B"));
        await new AuthorizationBootstrapper(
            context,
            new FixedTimeProvider(UtcNow)).EnsureAsync();

        using var scope = tenantContext.BeginScope(centerA);
        var sut = new AuthorizationSnapshotReader(context);

        var snapshot = await sut.ReadForUserAsync(managerA);
        var crossTenantSnapshot = await sut.ReadForUserAsync(managerB);

        var role = Assert.Single(snapshot.Roles);
        Assert.Equal("SYSTEM_CENTERMANAGER", role.RoleCode);
        Assert.Equal(nameof(UserRole.CenterManager), role.AccountType);
        Assert.Contains("authorization.permissions.read", snapshot.Permissions);
        Assert.Contains("authorization.roles.manage_permissions", snapshot.Permissions);
        Assert.Contains("organization.students.read", snapshot.Permissions);
        Assert.DoesNotContain("learning.attempts.submit", snapshot.Permissions);
        Assert.Equal(
            snapshot.Permissions.Order(StringComparer.Ordinal),
            snapshot.Permissions);
        Assert.Empty(crossTenantSnapshot.Roles);
        Assert.Empty(crossTenantSnapshot.Permissions);
    }

    [Fact]
    public async Task ReadForUserAsync_RevokedAssignment_ReturnsNoCapabilities()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        await using var context = CreateContext(tenantContext);
        await SeedBootstrapDataAsync(context, (centerId, managerId, "A"));
        await new AuthorizationBootstrapper(
            context,
            new FixedTimeProvider(UtcNow)).EnsureAsync();

        var assignment = await context.UserRoleAssignments
            .IgnoreQueryFilters()
            .SingleAsync(item => item.UserId == managerId);
        assignment.Status = UserRoleAssignmentStatus.Revoked;
        assignment.RevokedAt = UtcNow.UtcDateTime;
        assignment.RevokedByUserId = managerId;
        assignment.RevokeReason = "Regression test";
        await context.SaveChangesAsync();

        using var scope = tenantContext.BeginScope(centerId);
        var snapshot = await new AuthorizationSnapshotReader(context)
            .ReadForUserAsync(managerId);

        Assert.Empty(snapshot.Roles);
        Assert.Empty(snapshot.Permissions);
    }

    private static EduTwinDbContext CreateContext(TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EduTwinDbContext(options, tenantContext);
    }

    private static async Task SeedBootstrapDataAsync(
        EduTwinDbContext context,
        params (Guid CenterId, Guid ManagerId, string Suffix)[] centers)
    {
        foreach (var item in centers)
        {
            context.Centers.Add(new Center
            {
                CenterId = item.CenterId,
                CenterCode = $"CENTER-{item.Suffix}",
                CenterName = $"Center {item.Suffix}",
                Status = CenterStatus.Active,
                Timezone = "Asia/Bangkok",
                CreatedAt = UtcNow.UtcDateTime,
                UpdatedAt = UtcNow.UtcDateTime
            });
            context.Users.Add(new User
            {
                UserId = item.ManagerId,
                CenterId = item.CenterId,
                Username = $"manager-{item.Suffix}",
                PasswordHash = "not-used",
                RoleName = UserRole.CenterManager,
                DisplayName = $"Manager {item.Suffix}",
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = UtcNow.UtcDateTime,
                UpdatedAt = UtcNow.UtcDateTime
            });
        }

        context.Permissions.AddRange(
            AuthorizationPermissionCatalog.CreatePermissions());
        context.PermissionAccountTypes.AddRange(
            AuthorizationPermissionCatalog.CreateAccountTypeMappings());
        await context.SaveChangesAsync();
    }
}
