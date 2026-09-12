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

public sealed class AuthorizationBootstrapperTests
{
    private static readonly DateTimeOffset BootstrapUtcNow =
        new(2026, 9, 9, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Catalog_MatchesNormativeV1Shape()
    {
        var permissions = AuthorizationPermissionCatalog.CreatePermissions();
        var mappings = AuthorizationPermissionCatalog.CreateAccountTypeMappings();

        Assert.Equal(66, permissions.Count);
        Assert.Equal(66, permissions.Select(item => item.PermissionCode).Distinct().Count());
        Assert.Equal(66, permissions.Select(item => item.PermissionId).Distinct().Count());
        Assert.Equal(105, mappings.Count);
        Assert.Contains(permissions, item => item.PermissionCode == "curriculum.questions.delete");
        Assert.Contains(permissions, item => item.PermissionCode == "twin.student.update_own");
        Assert.Contains(permissions, item => item.PermissionCode == "twin.student.update_scoped");
        Assert.Contains(permissions, item => item.PermissionCode == "recommendations.student.update_own");
        Assert.All(permissions, permission =>
        {
            Assert.Equal(
                permission.PermissionCode.Split('.')[^1],
                permission.ActionName);
            Assert.Contains(mappings, mapping => mapping.PermissionId == permission.PermissionId);
        });
    }

    [Fact]
    public void SystemRoleId_MatchesMigrationMd5Representation()
    {
        var centerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var roleId = AuthorizationPermissionCatalog.CreateSystemRoleId(
            centerId,
            UserRole.CenterManager);

        Assert.Equal(
            Guid.Parse("992b1708-785f-b81d-2693-5fa6e201ae72"),
            roleId);
    }

    [Fact]
    public async Task EnsureAsync_CreatesIdempotentBootstrapForEveryAccountType()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        await using var context = CreateContext();
        context.Centers.Add(CreateCenter(centerId));
        context.Users.AddRange(
            CreateUser(centerId, managerId, UserRole.CenterManager),
            CreateUser(centerId, Guid.NewGuid(), UserRole.Teacher),
            CreateUser(centerId, Guid.NewGuid(), UserRole.Student));
        context.Permissions.AddRange(AuthorizationPermissionCatalog.CreatePermissions());
        context.PermissionAccountTypes.AddRange(
            AuthorizationPermissionCatalog.CreateAccountTypeMappings());
        await context.SaveChangesAsync();

        var sut = new AuthorizationBootstrapper(
            context,
            new FixedTimeProvider(BootstrapUtcNow));
        await sut.EnsureAsync();
        await sut.EnsureAsync();

        Assert.Equal(3, await context.AuthorizationRoles.IgnoreQueryFilters().CountAsync());
        Assert.Equal(102, await context.RolePermissions.IgnoreQueryFilters().CountAsync());
        Assert.Equal(3, await context.UserRoleAssignments.IgnoreQueryFilters().CountAsync());
        Assert.Single(await context.AuthorizationAuditLogs.IgnoreQueryFilters().ToListAsync());
        Assert.All(
            await context.UserRoleAssignments.IgnoreQueryFilters().ToArrayAsync(),
            assignment =>
            {
                Assert.Equal(UserRoleAssignmentStatus.Active, assignment.Status);
                Assert.Equal(BootstrapUtcNow.UtcDateTime, assignment.AssignedAt);
            });
        Assert.All(
            await context.AuthorizationRoles.IgnoreQueryFilters().ToArrayAsync(),
            role => Assert.Equal(BootstrapUtcNow.UtcDateTime, role.CreatedAt));
        Assert.Equal(
            BootstrapUtcNow.UtcDateTime,
            (await context.AuthorizationAuditLogs.IgnoreQueryFilters().SingleAsync()).CreatedAt);
    }

    [Fact]
    public async Task EnsureAsync_WithoutActiveCenterManager_FailsClosed()
    {
        var centerId = Guid.NewGuid();
        await using var context = CreateContext();
        context.Centers.Add(CreateCenter(centerId));
        context.Users.Add(CreateUser(centerId, Guid.NewGuid(), UserRole.Teacher));
        context.Permissions.AddRange(AuthorizationPermissionCatalog.CreatePermissions());
        context.PermissionAccountTypes.AddRange(
            AuthorizationPermissionCatalog.CreateAccountTypeMappings());
        await context.SaveChangesAsync();

        var sut = new AuthorizationBootstrapper(
            context,
            new FixedTimeProvider(BootstrapUtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnsureAsync());
        Assert.Empty(await context.AuthorizationRoles.IgnoreQueryFilters().ToArrayAsync());
    }

    [Fact]
    public void Model_ContainsAccountTypeCompositeForeignKeys()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(UserRoleAssignment));

        Assert.NotNull(entity);
        var foreignKeys = entity.GetForeignKeys().ToArray();
        Assert.Contains(foreignKeys, foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(["CenterId", "UserId", "AccountType"]));
        Assert.Contains(foreignKeys, foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(["CenterId", "RoleId", "AccountType"]));
    }

    private static EduTwinDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EduTwinDbContext(options, new TenantContext());
    }

    private static Center CreateCenter(Guid centerId) => new()
    {
        CenterId = centerId,
        CenterCode = $"C-{centerId:N}"[..20],
        CenterName = "Authorization test center",
        Status = CenterStatus.Active,
        Timezone = "Asia/Bangkok",
        CreatedAt = AuthorizationPermissionCatalog.CatalogTimestampUtc,
        UpdatedAt = AuthorizationPermissionCatalog.CatalogTimestampUtc
    };

    private static User CreateUser(Guid centerId, Guid userId, UserRole accountType) => new()
    {
        UserId = userId,
        CenterId = centerId,
        Username = $"user-{userId:N}",
        PasswordHash = "not-used-in-test",
        RoleName = accountType,
        DisplayName = accountType.ToString(),
        Status = UserStatus.Active,
        AuthVersion = 1,
        CreatedAt = AuthorizationPermissionCatalog.CatalogTimestampUtc,
        UpdatedAt = AuthorizationPermissionCatalog.CatalogTimestampUtc
    };
}
