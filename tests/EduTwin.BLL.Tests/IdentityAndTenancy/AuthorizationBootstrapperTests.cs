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

        Assert.Equal(71, permissions.Count);
        Assert.Equal(71, permissions.Select(item => item.PermissionCode).Distinct().Count());
        Assert.Equal(71, permissions.Select(item => item.PermissionId).Distinct().Count());
        Assert.Equal(80, mappings.Count);
        Assert.Contains(permissions, item => item.PermissionCode == "curriculum.questions.delete");
        Assert.Contains(permissions, item => item.PermissionCode == "twin.student.update_own");
        Assert.Contains(permissions, item => item.PermissionCode == "twin.student.update_scoped");
        Assert.Contains(permissions, item => item.PermissionCode == "recommendations.student.update_own");
        Assert.Contains(permissions, item => item.PermissionCode == "platform.audit.read");
        Assert.Contains(permissions, item => item.PermissionCode == "platform.account.manage_own");
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
        Assert.Equal(75, await context.RolePermissions.IgnoreQueryFilters().CountAsync());
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
    public void DefaultPermissions_CoverAllFourActors_WithoutCrossActorPrivileges()
    {
        var catalog = AuthorizationPermissionCatalog.CreatePermissions().ToDictionary(p => p.PermissionId);
        var mappings = AuthorizationPermissionCatalog.CreateAccountTypeMappings();
        foreach (var actor in Enum.GetValues<UserRole>())
        {
            var compatible = mappings.Where(m => m.AccountType == actor)
                .Select(m => catalog[m.PermissionId].PermissionCode).Order().ToArray();
            Assert.All(AuthorizationPermissionCatalog.GetDefaultSystemRoleCodes(actor), code => Assert.Contains(code, compatible));
        }
        Assert.Equal(5, AuthorizationPermissionCatalog.SystemPlatformAdminDefaultCodes.Count);
        Assert.Equal(31, AuthorizationPermissionCatalog.SystemCenterManagerDefaultCodes.Count);
        Assert.Equal(12, AuthorizationPermissionCatalog.SystemStudentDefaultCodes.Count);
        Assert.Equal(32, AuthorizationPermissionCatalog.SystemTeacherDefaultCodes.Count);
        Assert.All(AuthorizationPermissionCatalog.SensitiveSharedAcademicCodes,
            code => Assert.Contains(code, AuthorizationPermissionCatalog.SystemTeacherDefaultCodes));
        Assert.DoesNotContain(AuthorizationPermissionCatalog.SystemTeacherDefaultCodes, p => p.StartsWith("platform.") || p.StartsWith("authorization."));
        Assert.DoesNotContain(AuthorizationPermissionCatalog.SystemCenterManagerDefaultCodes, p => AuthorizationPermissionCatalog.AcademicOperationalCodes.Contains(p));
        Assert.DoesNotContain(AuthorizationPermissionCatalog.SystemStudentDefaultCodes, p => p.EndsWith("read_scoped") || p.EndsWith("override") || p.EndsWith("create") || p.EndsWith("delete"));
        Assert.All(AuthorizationPermissionCatalog.SystemPlatformAdminDefaultCodes, p => Assert.StartsWith("platform.", p));
    }

    [Fact]
    public async Task MissingTeacherGraphGrants_RefreshOnlyActiveRoleHolders_Idempotently()
    {
        await using var context = CreateContext();
        var centerId = Guid.NewGuid(); var otherCenterId = Guid.NewGuid();
        var teacher = CreateUser(centerId, Guid.NewGuid(), UserRole.Teacher);
        var revokedTeacher = CreateUser(centerId, Guid.NewGuid(), UserRole.Teacher);
        var otherTeacher = CreateUser(otherCenterId, Guid.NewGuid(), UserRole.Teacher);
        var manager = CreateUser(centerId, Guid.NewGuid(), UserRole.CenterManager);
        var student = CreateUser(centerId, Guid.NewGuid(), UserRole.Student);
        context.Centers.AddRange(CreateCenter(centerId), CreateCenter(otherCenterId));
        context.Users.AddRange(teacher, revokedTeacher, otherTeacher, manager, student,
            CreateUser(otherCenterId, Guid.NewGuid(), UserRole.CenterManager));
        var sut = new AuthorizationBootstrapper(context, new FixedTimeProvider(BootstrapUtcNow));
        await context.SaveChangesAsync(); await sut.EnsureAsync();
        var role = await context.AuthorizationRoles.IgnoreQueryFilters()
            .SingleAsync(r => r.CenterId == centerId && r.AccountType == UserRole.Teacher);
        context.RolePermissions.RemoveRange(await context.RolePermissions.IgnoreQueryFilters()
            .Where(p => p.RoleId == role.RoleId &&
                AuthorizationPermissionCatalog.SensitiveSharedAcademicCodes.Select(AuthorizationPermissionCatalog.CreateDeterministicId).Contains(p.PermissionId))
            .ToArrayAsync());
        var revoked = await context.UserRoleAssignments.IgnoreQueryFilters().SingleAsync(a => a.UserId == revokedTeacher.UserId);
        revoked.Status = UserRoleAssignmentStatus.Revoked;
        await context.SaveChangesAsync();
        var previousRoleVersion = role.RowVersion;

        await sut.EnsureCenterAsync(centerId);
        Assert.Equal(32, await context.RolePermissions.IgnoreQueryFilters().CountAsync(p => p.RoleId == role.RoleId));
        Assert.Equal(2u, teacher.AuthVersion);
        Assert.Equal(1u, revokedTeacher.AuthVersion);
        Assert.Equal(1u, otherTeacher.AuthVersion);
        Assert.Equal(1u, manager.AuthVersion);
        Assert.Equal(1u, student.AuthVersion);
        Assert.Equal(UserRoleAssignmentStatus.Revoked, revoked.Status);
        Assert.Equal(previousRoleVersion + 1, role.RowVersion);

        await sut.EnsureCenterAsync(centerId);
        Assert.Equal(2u, teacher.AuthVersion);
        Assert.Equal(previousRoleVersion + 1, role.RowVersion);
        Assert.Equal(32, await context.RolePermissions.IgnoreQueryFilters().CountAsync(p => p.RoleId == role.RoleId));
    }

    [Fact]
    public async Task MissingPlatformGrant_RefreshesAdminSessionOnce_AndKeepsFivePlatformOnlyPermissions()
    {
        await using var context = CreateContext();
        var centerId = AuthorizationBootstrapper.ReservedPlatformCenterId;
        var admin = CreateUser(centerId, Guid.NewGuid(), UserRole.PlatformAdmin);
        context.Centers.Add(CreateCenter(centerId)); context.Users.Add(admin);
        context.Permissions.AddRange(AuthorizationPermissionCatalog.CreatePermissions());
        context.PermissionAccountTypes.AddRange(AuthorizationPermissionCatalog.CreateAccountTypeMappings());
        await context.SaveChangesAsync();
        var sut = new AuthorizationBootstrapper(context, new FixedTimeProvider(BootstrapUtcNow));
        await sut.BootstrapPlatformAsync(centerId, admin.UserId);
        var role = await context.AuthorizationRoles.IgnoreQueryFilters().SingleAsync();
        var grant = await context.RolePermissions.IgnoreQueryFilters()
            .SingleAsync(p => p.PermissionId == AuthorizationPermissionCatalog.CreateDeterministicId("platform.audit.read"));
        context.RolePermissions.Remove(grant); await context.SaveChangesAsync();
        await sut.BootstrapPlatformAsync(centerId, admin.UserId);
        await sut.BootstrapPlatformAsync(centerId, admin.UserId);
        Assert.Equal(2u, admin.AuthVersion);
        Assert.Equal(5, await context.RolePermissions.IgnoreQueryFilters().CountAsync(p => p.RoleId == role.RoleId));
    }

    [Fact]
    public async Task StartupBackfill_WithoutDemoSeeder_IsAdditiveAudited_AndPreservesRevokedAndCustomRoles()
    {
        await using var context = CreateContext();
        var centerId = Guid.NewGuid();
        var teacher = CreateUser(centerId, Guid.NewGuid(), UserRole.Teacher);
        var revokedTeacher = CreateUser(centerId, Guid.NewGuid(), UserRole.Teacher);
        var student = CreateUser(centerId, Guid.NewGuid(), UserRole.Student);
        var manager = CreateUser(centerId, Guid.NewGuid(), UserRole.CenterManager);
        context.Centers.Add(CreateCenter(centerId)); context.Users.AddRange(teacher, revokedTeacher, student, manager);
        await context.SaveChangesAsync();
        await new AuthorizationBootstrapper(context, new FixedTimeProvider(BootstrapUtcNow)).EnsureAsync();
        var teacherRole = await context.AuthorizationRoles.IgnoreQueryFilters().SingleAsync(r => r.AccountType == UserRole.Teacher);
        var studentRole = await context.AuthorizationRoles.IgnoreQueryFilters().SingleAsync(r => r.AccountType == UserRole.Student);
        var missingTeacherIds = AuthorizationPermissionCatalog.SensitiveSharedAcademicCodes
            .Select(AuthorizationPermissionCatalog.CreateDeterministicId).ToArray();
        context.RolePermissions.RemoveRange(await context.RolePermissions.IgnoreQueryFilters()
            .Where(p => p.RoleId == teacherRole.RoleId && missingTeacherIds.Contains(p.PermissionId)).ToArrayAsync());
        context.RolePermissions.Remove(await context.RolePermissions.IgnoreQueryFilters().SingleAsync(p =>
            p.RoleId == studentRole.RoleId && p.PermissionId == AuthorizationPermissionCatalog.CreateDeterministicId("dashboards.student.read_own")));
        var revokedAssignment = await context.UserRoleAssignments.IgnoreQueryFilters().SingleAsync(a => a.UserId == revokedTeacher.UserId);
        revokedAssignment.Status = UserRoleAssignmentStatus.Revoked;
        var customRole = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(), CenterId = centerId, AccountType = UserRole.Teacher,
            RoleCode = "CUSTOM_READER", RoleName = "Reader", IsSystemRole = false,
            Status = AuthorizationRoleStatus.Active, CreatedAt = BootstrapUtcNow.UtcDateTime, UpdatedAt = BootstrapUtcNow.UtcDateTime
        };
        context.AuthorizationRoles.Add(customRole);
        context.RolePermissions.Add(new RolePermission { CenterId = centerId, RoleId = customRole.RoleId,
            AccountType = UserRole.Teacher, PermissionId = AuthorizationPermissionCatalog.CreateDeterministicId("knowledge.nodes.read"),
            GrantedAt = BootstrapUtcNow.UtcDateTime });
        await context.SaveChangesAsync();

        var sut = new DefaultSystemRolePermissionBackfill(context, new FixedTimeProvider(BootstrapUtcNow));
        await sut.EnsureAsync(); await sut.EnsureAsync();
        Assert.Equal(32, await context.RolePermissions.IgnoreQueryFilters().CountAsync(p => p.RoleId == teacherRole.RoleId));
        Assert.Equal(12, await context.RolePermissions.IgnoreQueryFilters().CountAsync(p => p.RoleId == studentRole.RoleId));
        Assert.Equal(1, await context.RolePermissions.IgnoreQueryFilters().CountAsync(p => p.RoleId == customRole.RoleId));
        Assert.Equal(2u, teacher.AuthVersion); Assert.Equal(2u, student.AuthVersion);
        Assert.Equal(1u, manager.AuthVersion); Assert.Equal(1u, revokedTeacher.AuthVersion);
        Assert.Equal(UserRoleAssignmentStatus.Revoked, revokedAssignment.Status);
        var logs = await context.AuthorizationAuditLogs.IgnoreQueryFilters()
            .Where(a => a.ActionType == "SystemRoleDefaultPermissionsGranted").ToArrayAsync();
        Assert.Equal(2, logs.Length);
        Assert.Contains(logs, a => a.TargetId == teacherRole.RoleId.ToString("D") && a.AfterData!.Contains("knowledge.edges.create"));
        Assert.Equal(4, await context.UserRoleAssignments.IgnoreQueryFilters().CountAsync());
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
