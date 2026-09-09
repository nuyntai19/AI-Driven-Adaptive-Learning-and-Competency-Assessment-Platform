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

public sealed class AuthorizationRoleUseCaseTests
{
    [Fact]
    public async Task CreateGetAndListRole_AreTenantScopedAndAudited()
    {
        await using var fixture = await CreateFixtureAsync();
        var create = new CreateAuthorizationRoleUseCase(
            fixture.Context,
            fixture.TenantContext,
            TimeProvider.System);

        var created = await create.ExecuteAsync(new CreateAuthorizationRoleRequest
        {
            RoleCode = "ACADEMIC_COORDINATOR",
            RoleName = " Điều phối học thuật ",
            AccountType = UserRole.Teacher,
            Description = " Quản lý nội dung "
        }, "trace-create");

        Assert.True(created.IsSuccess);
        Assert.Equal("Điều phối học thuật", created.Data!.RoleName);
        Assert.Equal(nameof(UserRole.Teacher), created.Data.AccountType);
        Assert.Equal("1", created.Data.RowVersion);
        Assert.Contains(await fixture.Context.AuthorizationAuditLogs.ToListAsync(),
            audit => audit.ActionType == "RoleCreated" &&
                     audit.TargetId == created.Data.RoleId.ToString("D"));

        var get = new GetAuthorizationRoleUseCase(
            fixture.Context,
            fixture.TenantContext);
        var detail = await get.ExecuteAsync(created.Data.RoleId);
        Assert.True(detail.IsSuccess);
        Assert.Equal("ACADEMIC_COORDINATOR", detail.Data!.RoleCode);

        var list = new ListAuthorizationRolesUseCase(
            fixture.Context,
            fixture.TenantContext);
        var page = await list.ExecuteAsync(new AuthorizationRoleListQuery
        {
            Search = "ACADEMIC",
            AccountType = UserRole.Teacher
        });
        Assert.True(page.IsSuccess);
        Assert.Single(page.Data);
        Assert.Equal(created.Data.RoleId, page.Data[0].RoleId);
    }

    [Fact]
    public async Task CreateRole_DuplicateCode_ReturnsStableConflict()
    {
        await using var fixture = await CreateFixtureAsync();
        var sut = new CreateAuthorizationRoleUseCase(
            fixture.Context,
            fixture.TenantContext,
            TimeProvider.System);
        var request = new CreateAuthorizationRoleRequest
        {
            RoleCode = "CONTENT_EDITOR",
            RoleName = "Biên tập nội dung",
            AccountType = UserRole.Teacher
        };

        Assert.True((await sut.ExecuteAsync(request, "trace-1")).IsSuccess);
        var duplicate = await sut.ExecuteAsync(request, "trace-2");

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, duplicate.ErrorCode);
    }

    [Fact]
    public async Task GetRole_CrossTenantIdentifier_ReturnsNotFound()
    {
        await using var fixture = await CreateFixtureAsync();
        var otherRole = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = Guid.NewGuid(),
            RoleCode = "OTHER_CENTER",
            RoleName = "Other",
            AccountType = UserRole.Teacher,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        fixture.Context.AuthorizationRoles.Add(otherRole);
        await fixture.Context.SaveChangesAsync();
        var sut = new GetAuthorizationRoleUseCase(
            fixture.Context,
            fixture.TenantContext);

        var result = await sut.ExecuteAsync(otherRole.RoleId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateRole_UsesRowVersionAndWritesAudit()
    {
        await using var fixture = await CreateFixtureAsync();
        var role = await AddCustomRoleAsync(fixture, UserRole.Teacher);
        var guard = new Mock<ITenantAdministratorGuard>();
        guard.Setup(item => item.HasAdministratorAfterAsync(
                It.IsAny<Guid?>(), It.IsAny<bool?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var evaluator = new Mock<IPermissionEvaluator>();
        var sut = new UpdateAuthorizationRoleUseCase(
            fixture.Context,
            fixture.TenantContext,
            guard.Object,
            evaluator.Object,
            TimeProvider.System);

        var result = await sut.ExecuteAsync(role.RoleId,
            new UpdateAuthorizationRoleRequest
            {
                RoleName = "Tên mới",
                Description = "Phạm vi mới",
                Status = AuthorizationRoleStatus.Active,
                RowVersion = "1",
                Reason = "Điều chỉnh nhiệm vụ"
            },
            "trace-update");

        Assert.True(result.IsSuccess);
        Assert.Equal("2", result.Data!.RowVersion);
        Assert.Equal("Tên mới", result.Data.RoleName);
        Assert.Contains(await fixture.Context.AuthorizationAuditLogs.ToListAsync(),
            audit => audit.ActionType == "RoleUpdated" &&
                     audit.Reason == "Điều chỉnh nhiệm vụ");

        var stale = await sut.ExecuteAsync(role.RoleId,
            new UpdateAuthorizationRoleRequest
            {
                RoleName = "Stale",
                Status = AuthorizationRoleStatus.Active,
                RowVersion = "1",
                Reason = "Stale request"
            },
            "trace-stale");
        Assert.False(stale.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, stale.ErrorCode);
    }

    [Fact]
    public async Task ArchiveRole_RequiresArchivePermissionAndPreservesLastAdmin()
    {
        await using var fixture = await CreateFixtureAsync();
        var role = await AddCustomRoleAsync(fixture, UserRole.CenterManager);
        var guard = new Mock<ITenantAdministratorGuard>();
        guard.Setup(item => item.HasAdministratorAfterAsync(
                role.RoleId, false, null, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var evaluator = new Mock<IPermissionEvaluator>();
        evaluator.Setup(item => item.HasPermissionAsync(
                "authorization.roles.archive",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new UpdateAuthorizationRoleUseCase(
            fixture.Context,
            fixture.TenantContext,
            guard.Object,
            evaluator.Object,
            TimeProvider.System);

        var result = await sut.ExecuteAsync(role.RoleId,
            new UpdateAuthorizationRoleRequest
            {
                RoleName = role.RoleName,
                Status = AuthorizationRoleStatus.Archived,
                RowVersion = "1",
                Reason = "Ngừng sử dụng role"
            },
            "trace-archive");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.LastTenantAdmin, result.ErrorCode);
        Assert.Equal(AuthorizationRoleStatus.Active,
            (await fixture.Context.AuthorizationRoles.SingleAsync(
                item => item.RoleId == role.RoleId)).Status);
    }

    [Fact]
    public async Task ArchiveSystemRole_IsProtected()
    {
        await using var fixture = await CreateFixtureAsync();
        var role = await fixture.Context.AuthorizationRoles.SingleAsync(
            item => item.IsSystemRole && item.AccountType == UserRole.CenterManager);
        var evaluator = new Mock<IPermissionEvaluator>();
        evaluator.Setup(item => item.HasPermissionAsync(
                "authorization.roles.archive",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new UpdateAuthorizationRoleUseCase(
            fixture.Context,
            fixture.TenantContext,
            Mock.Of<ITenantAdministratorGuard>(),
            evaluator.Object,
            TimeProvider.System);

        var result = await sut.ExecuteAsync(role.RoleId,
            new UpdateAuthorizationRoleRequest
            {
                RoleName = role.RoleName,
                Description = role.Description,
                Status = AuthorizationRoleStatus.Archived,
                RowVersion = role.RowVersion.ToString(),
                Reason = "Không còn dùng"
            },
            "trace-system");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task ReplaceRolePermissions_IsAtomicAndInvalidatesAffectedSessions()
    {
        await using var fixture = await CreateFixtureAsync();
        var role = await AddCustomRoleAsync(fixture, UserRole.Teacher);
        var teacherId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        fixture.Context.Users.Add(new User
        {
            UserId = teacherId,
            CenterId = fixture.TenantContext.CenterId!.Value,
            Username = "teacher-rbac",
            PasswordHash = "not-used",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher",
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        });
        fixture.Context.UserRoleAssignments.Add(new UserRoleAssignment
        {
            CenterId = fixture.TenantContext.CenterId.Value,
            UserId = teacherId,
            RoleId = role.RoleId,
            AccountType = UserRole.Teacher,
            Status = UserRoleAssignmentStatus.Active,
            AssignedAt = now,
            AssignedByUserId = fixture.TenantContext.UserId!.Value,
            RowVersion = 1
        });
        fixture.Context.RefreshTokens.Add(new RefreshToken
        {
            CenterId = fixture.TenantContext.CenterId.Value,
            UserId = teacherId,
            TokenHash = new string('a', 64),
            ExpiresAt = now.AddDays(1),
            CreatedAt = now
        });
        var existingPermission = await fixture.Context.Permissions.SingleAsync(
            permission => permission.PermissionCode == "knowledge.nodes.update");
        fixture.Context.RolePermissions.Add(new RolePermission
        {
            CenterId = fixture.TenantContext.CenterId.Value,
            RoleId = role.RoleId,
            PermissionId = existingPermission.PermissionId,
            AccountType = UserRole.Teacher,
            GrantedAt = now,
            GrantedByUserId = fixture.TenantContext.UserId.Value
        });
        await fixture.Context.SaveChangesAsync();
        var guard = new Mock<ITenantAdministratorGuard>();
        guard.Setup(item => item.HasAdministratorAfterAsync(
                It.IsAny<Guid?>(), It.IsAny<bool?>(),
                It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyCollection<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new ReplaceRolePermissionsUseCase(
            fixture.Context,
            fixture.TenantContext,
            new AuthorizationSnapshotReader(fixture.Context),
            guard.Object,
            TimeProvider.System);

        var result = await sut.ExecuteAsync(role.RoleId,
            new ReplaceRolePermissionsRequest
            {
                PermissionCodes =
                [
                    "dashboards.teacher.read_scoped",
                    "knowledge.nodes.update"
                ],
                RowVersion = "1",
                Reason = "Cấp quyền giảng dạy"
            },
            "trace-permissions");

        Assert.True(result.IsSuccess);
        Assert.Equal("2", result.Data!.RowVersion);
        Assert.Equal(
            ["dashboards.teacher.read_scoped", "knowledge.nodes.update"],
            result.Data.PermissionCodes);
        var teacher = await fixture.Context.Users.SingleAsync(
            user => user.UserId == teacherId);
        Assert.Equal(2u, teacher.AuthVersion);
        Assert.Equal(2UL, teacher.RowVersion);
        Assert.NotNull((await fixture.Context.RefreshTokens.SingleAsync()).RevokedAt);
        Assert.Equal(2, await fixture.Context.RolePermissions.CountAsync(
            mapping => mapping.RoleId == role.RoleId));
        Assert.Contains(await fixture.Context.AuthorizationAuditLogs.ToListAsync(),
            audit => audit.ActionType == "RolePermissionsReplaced" &&
                     audit.TargetId == role.RoleId.ToString("D"));
    }

    [Fact]
    public async Task ReplaceRolePermissions_RejectsDuplicateAndAccountTypeMismatch()
    {
        await using var fixture = await CreateFixtureAsync();
        var role = await AddCustomRoleAsync(fixture, UserRole.Teacher);
        var sut = new ReplaceRolePermissionsUseCase(
            fixture.Context,
            fixture.TenantContext,
            new AuthorizationSnapshotReader(fixture.Context),
            Mock.Of<ITenantAdministratorGuard>(),
            TimeProvider.System);

        var duplicate = await sut.ExecuteAsync(role.RoleId,
            new ReplaceRolePermissionsRequest
            {
                PermissionCodes = ["knowledge.nodes.read", "knowledge.nodes.read"],
                RowVersion = "1",
                Reason = "Duplicate"
            },
            "trace-duplicate");
        Assert.Equal(ErrorCodes.ValidationFailed, duplicate.ErrorCode);

        var mismatch = await sut.ExecuteAsync(role.RoleId,
            new ReplaceRolePermissionsRequest
            {
                PermissionCodes = ["learning.attempts.submit"],
                RowVersion = "1",
                Reason = "Mismatch"
            },
            "trace-mismatch");
        Assert.Equal(ErrorCodes.RoleAccountTypeMismatch, mismatch.ErrorCode);
        Assert.Empty(await fixture.Context.RolePermissions
            .Where(mapping => mapping.RoleId == role.RoleId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ReplaceCenterManagerPermissions_BlocksOverGrantAndLastAdminLoss()
    {
        await using var fixture = await CreateFixtureAsync();
        var role = await AddCustomRoleAsync(fixture, UserRole.CenterManager);
        var snapshot = new Mock<IAuthorizationSnapshotReader>();
        snapshot.Setup(item => item.ReadForUserAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationSnapshot([], ["authorization.roles.read"]));
        var guard = new Mock<ITenantAdministratorGuard>();
        var sut = new ReplaceRolePermissionsUseCase(
            fixture.Context,
            fixture.TenantContext,
            snapshot.Object,
            guard.Object,
            TimeProvider.System);

        var overGrant = await sut.ExecuteAsync(role.RoleId,
            new ReplaceRolePermissionsRequest
            {
                PermissionCodes = ["authorization.roles.create"],
                RowVersion = "1",
                Reason = "Over grant"
            },
            "trace-overgrant");
        Assert.Equal(ErrorCodes.AuthPrivilegeEscalation, overGrant.ErrorCode);

        snapshot.Setup(item => item.ReadForUserAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorizationSnapshot(
                [],
                EduTwin.DAL.Seeding.AuthorizationPermissionCatalog
                    .TenantAdminCorePermissionsV1.ToArray()));
        guard.Setup(item => item.HasAdministratorAfterAsync(
                role.RoleId, true,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var lastAdmin = await sut.ExecuteAsync(role.RoleId,
            new ReplaceRolePermissionsRequest
            {
                PermissionCodes = EduTwin.DAL.Seeding.AuthorizationPermissionCatalog
                    .TenantAdminCorePermissionsV1.ToArray(),
                RowVersion = "1",
                Reason = "Would lose admin"
            },
            "trace-last-admin");
        Assert.Equal(ErrorCodes.LastTenantAdmin, lastAdmin.ErrorCode);
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.Initialize(
            centerId,
            managerId,
            nameof(UserRole.CenterManager),
            1);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new EduTwinDbContext(options, tenantContext);
        context.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = $"C-{centerId:N}"[..20],
            CenterName = "Role tests",
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
        await new AuthorizationBootstrapper(
            context,
            TimeProvider.System).EnsureAsync();
        return new Fixture(context, tenantContext);
    }

    private static async Task<AuthorizationRole> AddCustomRoleAsync(
        Fixture fixture,
        UserRole accountType)
    {
        var role = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = fixture.TenantContext.CenterId!.Value,
            RoleCode = $"CUSTOM_{Guid.NewGuid():N}"[..32].ToUpperInvariant(),
            RoleName = "Custom role",
            AccountType = accountType,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        fixture.Context.AuthorizationRoles.Add(role);
        await fixture.Context.SaveChangesAsync();
        return role;
    }

    private sealed record Fixture(
        EduTwinDbContext Context,
        TenantContext TenantContext) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
