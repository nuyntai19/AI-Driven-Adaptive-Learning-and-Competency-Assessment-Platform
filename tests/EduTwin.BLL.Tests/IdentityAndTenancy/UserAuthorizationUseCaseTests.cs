using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
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

public sealed class UserAuthorizationUseCaseTests
{
    [Fact]
    public async Task GetUserRoles_ReturnsEffectiveAuthorizationAndHidesCrossTenantUser()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new GetUserAuthorizationUseCase(
            fixture.Context,
            fixture.TenantContext,
            new AuthorizationSnapshotReader(fixture.Context));

        var result = await sut.ExecuteAsync(fixture.ManagerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(UserRole.CenterManager), result.Data!.AccountType);
        Assert.Single(result.Data.Roles,
            role => role.AssignmentStatus == nameof(UserRoleAssignmentStatus.Active));
        Assert.Contains("authorization.user_roles.assign", result.Data.Permissions);

        var crossTenant = await sut.ExecuteAsync(Guid.NewGuid());
        Assert.False(crossTenant.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, crossTenant.ErrorCode);
    }

    [Fact]
    public async Task ReplaceUserRoles_RevokesOldAssignmentTokenAndBumpsVersions()
    {
        await using var fixture = await CreateFixtureAsync();
        var (teacher, oldRole, newRole) = await SeedTeacherRolesAsync(fixture);
        var guard = new Mock<ITenantAdministratorGuard>();
        var sut = new ReplaceUserRolesUseCase(
            fixture.Context,
            fixture.TenantContext,
            new AuthorizationSnapshotReader(fixture.Context),
            guard.Object,
            TimeProvider.System);

        var result = await sut.ExecuteAsync(teacher.UserId,
            new ReplaceUserRolesRequest
            {
                RoleIds = [newRole.RoleId],
                RowVersion = "1",
                Reason = "Chuyển nhiệm vụ giảng dạy"
            },
            "trace-user-role");

        Assert.True(result.IsSuccess);
        Assert.Equal(2u, result.Data!.AuthorizationVersion);
        Assert.Equal("2", result.Data.RowVersion);
        Assert.Contains(result.Data.Roles, role =>
            role.RoleId == oldRole.RoleId &&
            role.AssignmentStatus == nameof(UserRoleAssignmentStatus.Revoked));
        Assert.Contains(result.Data.Roles, role =>
            role.RoleId == newRole.RoleId &&
            role.AssignmentStatus == nameof(UserRoleAssignmentStatus.Active));
        Assert.Contains("knowledge.nodes.update", result.Data.Permissions);
        Assert.NotNull((await fixture.Context.RefreshTokens.SingleAsync()).RevokedAt);
        Assert.Contains(await fixture.Context.AuthorizationAuditLogs.ToListAsync(),
            audit => audit.ActionType == "UserRolesReplaced" &&
                     audit.TargetUserId == teacher.UserId);
    }

    [Fact]
    public async Task ReplaceUserRoles_RejectsStaleVersionAndAccountTypeMismatch()
    {
        await using var fixture = await CreateFixtureAsync();
        var (teacher, _, _) = await SeedTeacherRolesAsync(fixture);
        var studentRole = await fixture.Context.AuthorizationRoles.SingleAsync(
            role => role.IsSystemRole && role.AccountType == UserRole.Student);
        var sut = new ReplaceUserRolesUseCase(
            fixture.Context,
            fixture.TenantContext,
            new AuthorizationSnapshotReader(fixture.Context),
            Mock.Of<ITenantAdministratorGuard>(),
            TimeProvider.System);

        var stale = await sut.ExecuteAsync(teacher.UserId,
            new ReplaceUserRolesRequest
            {
                RoleIds = [],
                RowVersion = "999",
                Reason = "Stale"
            },
            "trace-stale");
        Assert.Equal(ErrorCodes.ConcurrencyConflict, stale.ErrorCode);

        var mismatch = await sut.ExecuteAsync(teacher.UserId,
            new ReplaceUserRolesRequest
            {
                RoleIds = [studentRole.RoleId],
                RowVersion = "1",
                Reason = "Mismatch"
            },
            "trace-mismatch");
        Assert.Equal(ErrorCodes.RoleAccountTypeMismatch, mismatch.ErrorCode);
    }

    [Fact]
    public async Task ReplaceCenterManagerRoles_PreservesLastAdministrator()
    {
        await using var fixture = await CreateFixtureAsync();
        var guard = new Mock<ITenantAdministratorGuard>();
        guard.Setup(item => item.HasAdministratorAfterAsync(
                null, null, null, fixture.ManagerId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = new ReplaceUserRolesUseCase(
            fixture.Context,
            fixture.TenantContext,
            new AuthorizationSnapshotReader(fixture.Context),
            guard.Object,
            TimeProvider.System);
        var manager = await fixture.Context.Users.SingleAsync(
            user => user.UserId == fixture.ManagerId);

        var result = await sut.ExecuteAsync(manager.UserId,
            new ReplaceUserRolesRequest
            {
                RoleIds = [],
                RowVersion = manager.RowVersion.ToString(),
                Reason = "Không hợp lệ"
            },
            "trace-last-admin");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.LastTenantAdmin, result.ErrorCode);
        Assert.Equal(1u, manager.AuthVersion);
    }

    [Fact]
    public async Task TenantAdministratorGuard_SimulatesRoleArchive()
    {
        await using var fixture = await CreateFixtureAsync();
        var systemRole = await fixture.Context.AuthorizationRoles.SingleAsync(
            role => role.IsSystemRole && role.AccountType == UserRole.CenterManager);
        var guard = new TenantAdministratorGuard(
            fixture.Context,
            fixture.TenantContext);

        Assert.True(await guard.HasAdministratorAfterAsync());
        Assert.False(await guard.HasAdministratorAfterAsync(
            changedRoleId: systemRole.RoleId,
            changedRoleActive: false));
    }

    [Fact]
    public async Task ListAudit_IsTenantScopedFilteredAndReturnsJsonSnapshots()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.UtcNow;
        fixture.Context.AuthorizationAuditLogs.AddRange(
            new AuthorizationAuditLog
            {
                CenterId = fixture.TenantContext.CenterId!.Value,
                ActorUserId = fixture.ManagerId,
                ActionType = "RoleUpdated",
                TargetType = "Role",
                TargetId = "role-a",
                BeforeData = "{\"status\":\"Active\"}",
                AfterData = "{\"status\":\"Archived\"}",
                Reason = "Audit test",
                TraceId = "trace-a",
                CreatedAt = now,
                CreatedBy = fixture.ManagerId
            },
            new AuthorizationAuditLog
            {
                CenterId = Guid.NewGuid(),
                ActionType = "RoleUpdated",
                TargetType = "Role",
                TargetId = "role-b",
                Reason = "Other center",
                TraceId = "trace-b",
                CreatedAt = now
            });
        await fixture.Context.SaveChangesAsync();
        var sut = new ListAuthorizationAuditUseCase(
            fixture.Context,
            fixture.TenantContext);

        var result = await sut.ExecuteAsync(new AuthorizationAuditQuery
        {
            ActionType = "RoleUpdated",
            From = now.AddMinutes(-1),
            To = now.AddMinutes(1)
        });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data);
        Assert.Equal("Active", result.Data[0].Before!.Value.GetProperty("status").GetString());
        Assert.Equal("Archived", result.Data[0].After!.Value.GetProperty("status").GetString());
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.Initialize(centerId, managerId, nameof(UserRole.CenterManager), 1);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new EduTwinDbContext(options, tenantContext);
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = $"C-{centerId:N}"[..20],
            CenterName = "User role tests",
            Status = CenterStatus.Active,
            Timezone = "Asia/Bangkok",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Users.Add(new User
        {
            UserId = managerId,
            CenterId = centerId,
            Username = "manager",
            PasswordHash = "not-used",
            RoleName = UserRole.CenterManager,
            DisplayName = "Manager",
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        });
        context.Permissions.AddRange(AuthorizationPermissionCatalog.CreatePermissions());
        context.PermissionAccountTypes.AddRange(
            AuthorizationPermissionCatalog.CreateAccountTypeMappings());
        await context.SaveChangesAsync();
        await new AuthorizationBootstrapper(context, TimeProvider.System).EnsureAsync();
        return new Fixture(context, tenantContext, managerId);
    }

    private static async Task<(User Teacher, AuthorizationRole OldRole, AuthorizationRole NewRole)>
        SeedTeacherRolesAsync(Fixture fixture)
    {
        var now = DateTime.UtcNow;
        var teacher = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = fixture.TenantContext.CenterId!.Value,
            Username = "teacher",
            PasswordHash = "not-used",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher",
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        var oldRole = await fixture.Context.AuthorizationRoles.SingleAsync(
            role => role.IsSystemRole && role.AccountType == UserRole.Teacher);
        var newRole = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = fixture.TenantContext.CenterId.Value,
            RoleCode = "TEACHER_CONTENT_EDITOR",
            RoleName = "Biên tập nội dung",
            AccountType = UserRole.Teacher,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        var permission = await fixture.Context.Permissions.SingleAsync(
            item => item.PermissionCode == "knowledge.nodes.update");
        fixture.Context.Users.Add(teacher);
        fixture.Context.AuthorizationRoles.Add(newRole);
        fixture.Context.UserRoleAssignments.Add(new UserRoleAssignment
        {
            CenterId = fixture.TenantContext.CenterId.Value,
            UserId = teacher.UserId,
            RoleId = oldRole.RoleId,
            AccountType = UserRole.Teacher,
            Status = UserRoleAssignmentStatus.Active,
            AssignedAt = now,
            AssignedByUserId = fixture.ManagerId,
            RowVersion = 1
        });
        fixture.Context.RolePermissions.Add(new RolePermission
        {
            CenterId = fixture.TenantContext.CenterId.Value,
            RoleId = newRole.RoleId,
            PermissionId = permission.PermissionId,
            AccountType = UserRole.Teacher,
            GrantedAt = now,
            GrantedByUserId = fixture.ManagerId
        });
        fixture.Context.RefreshTokens.Add(new RefreshToken
        {
            CenterId = fixture.TenantContext.CenterId.Value,
            UserId = teacher.UserId,
            TokenHash = new string('b', 64),
            ExpiresAt = now.AddDays(1),
            CreatedAt = now
        });
        await fixture.Context.SaveChangesAsync();
        return (teacher, oldRole, newRole);
    }

    private sealed record Fixture(
        EduTwinDbContext Context,
        TenantContext TenantContext,
        Guid ManagerId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
