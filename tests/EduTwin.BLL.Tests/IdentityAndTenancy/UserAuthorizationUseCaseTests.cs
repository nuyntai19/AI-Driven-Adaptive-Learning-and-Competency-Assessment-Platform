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
        Assert.Contains("authorization.user_roles.assign", result.Data.Roles[0].PermissionCodes);

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
    public async Task ListAuthorizationUsers_ReturnsAllCenterUsersAndSupportsSearchFilterAndPagination()
    {
        await using var fixture = await CreateFixtureAsync();
        var (teacher, _, _) = await SeedTeacherRolesAsync(fixture);
        var now = DateTime.UtcNow;

        var secondManager = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = fixture.TenantContext.CenterId!.Value,
            Username = "mgr.second",
            DisplayName = "Second Manager",
            PasswordHash = "hash",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            RowVersion = 1,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var student = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = fixture.TenantContext.CenterId!.Value,
            Username = "student.01",
            DisplayName = "Student Alpha",
            PasswordHash = "hash",
            RoleName = UserRole.Student,
            Status = UserStatus.Active,
            RowVersion = 1,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        var crossTenantUser = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = Guid.NewGuid(),
            Username = "other.manager",
            DisplayName = "Other Manager",
            PasswordHash = "hash",
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            RowVersion = 1,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        fixture.Context.Users.AddRange(secondManager, student, crossTenantUser);
        await fixture.Context.SaveChangesAsync();

        var sut = new ListAuthorizationUsersUseCase(fixture.Context, fixture.TenantContext);

        // 1. List all users in center (cross-tenant hidden)
        var allResult = await sut.ExecuteAsync(new AuthorizationUserListQuery { Page = 1, PageSize = 20 });
        Assert.True(allResult.IsSuccess);
        Assert.DoesNotContain(allResult.Data, u => u.Username == "other.manager");
        Assert.Contains(allResult.Data, u => u.Username == "mgr.second");
        Assert.Contains(allResult.Data, u => u.Username == teacher.Username);
        Assert.Contains(allResult.Data, u => u.Username == "student.01");

        // 2. Filter by AccountType
        var mgrResult = await sut.ExecuteAsync(new AuthorizationUserListQuery { AccountType = UserRole.CenterManager });
        Assert.True(mgrResult.IsSuccess);
        Assert.All(mgrResult.Data, u => Assert.Equal(UserRole.CenterManager, u.AccountType));
        Assert.Contains(mgrResult.Data, u => u.Username == "mgr.second");
        Assert.DoesNotContain(mgrResult.Data, u => u.Username == teacher.Username);

        // 3. Search by username or display name
        var searchResult = await sut.ExecuteAsync(new AuthorizationUserListQuery { Search = "Alpha" });
        Assert.True(searchResult.IsSuccess);
        Assert.Single(searchResult.Data);
        Assert.Equal("student.01", searchResult.Data[0].Username);

        // 4. Pagination
        var pagedResult = await sut.ExecuteAsync(new AuthorizationUserListQuery { Page = 1, PageSize = 2 });
        Assert.True(pagedResult.IsSuccess);
        Assert.Equal(2, pagedResult.Data.Count);
        Assert.True(pagedResult.TotalItems >= 4);
    }

    [Fact]
    public async Task ListAuthorizationAudit_SupportsTargetIdAndPermissionCodeFilters()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.UtcNow;
        var audit1 = new AuthorizationAuditLog
        {
            CenterId = fixture.TenantContext.CenterId!.Value,
            ActorUserId = fixture.ManagerId,
            ActionType = "RoleCreated",
            TargetType = "Role",
            TargetId = "ROLE_TARGET_1",
            PermissionCode = "curriculum.curriculums.read",
            TraceId = "trace-1",
            Reason = "Lý do 1",
            CreatedAt = now
        };
        var audit2 = new AuthorizationAuditLog
        {
            CenterId = fixture.TenantContext.CenterId!.Value,
            ActorUserId = fixture.ManagerId,
            ActionType = "RolePermissionsReplaced",
            TargetType = "RolePermission",
            TargetId = "ROLE_TARGET_2",
            PermissionCode = null, // Real mutation producer leaves column null and records in AfterData JSON
            BeforeData = "{\"PermissionCodes\":[\"curriculum.curriculums.read\"]}",
            AfterData = "{\"PermissionCodes\":[\"assignments.assignments.create\",\"assignments.assignments.read\"]}",
            TraceId = "trace-2",
            Reason = "Lý do 2",
            CreatedAt = now
        };
        var audit3 = new AuthorizationAuditLog
        {
            CenterId = fixture.TenantContext.CenterId!.Value,
            ActorUserId = fixture.ManagerId,
            ActionType = "RolePermissionsReplaced",
            TargetType = "RolePermission",
            TargetId = "ROLE_TARGET_3",
            PermissionCode = null,
            BeforeData = null,
            AfterData = "{\"PermissionCodes\":[\"assignments.assignments.create_extended\"]}",
            TraceId = "trace-3",
            Reason = "Lý do 3",
            CreatedAt = now
        };
        var audit4 = new AuthorizationAuditLog
        {
            CenterId = fixture.TenantContext.CenterId!.Value,
            ActorUserId = fixture.ManagerId,
            ActionType = "RolePermissionsReplaced",
            TargetType = "RolePermission",
            TargetId = "ROLE_TARGET_4",
            PermissionCode = null,
            BeforeData = null,
            AfterData = "{\"PermissionCodes\":[\"prefix_assignments.assignments.create\"]}",
            TraceId = "trace-4",
            Reason = "Lý do 4",
            CreatedAt = now
        };
        fixture.Context.AuthorizationAuditLogs.AddRange(audit1, audit2, audit3, audit4);
        await fixture.Context.SaveChangesAsync();

        var sut = new ListAuthorizationAuditUseCase(fixture.Context, fixture.TenantContext);

        // Filter by TargetId
        var targetResult = await sut.ExecuteAsync(new AuthorizationAuditQuery { TargetId = "ROLE_TARGET_1" });
        Assert.True(targetResult.IsSuccess);
        Assert.Single(targetResult.Data);
        Assert.Equal("ROLE_TARGET_1", targetResult.Data[0].TargetId);

        // Filter by PermissionCode: must match exact code in AfterData and NOT match substring/prefix collision (audit3, audit4)
        var permResult = await sut.ExecuteAsync(new AuthorizationAuditQuery { PermissionCode = "assignments.assignments.create" });
        Assert.True(permResult.IsSuccess);
        Assert.Single(permResult.Data);
        Assert.Equal("ROLE_TARGET_2", permResult.Data[0].TargetId);

        // Filter by suffix-extended code matches audit3
        var extResult = await sut.ExecuteAsync(new AuthorizationAuditQuery { PermissionCode = "assignments.assignments.create_extended" });
        Assert.True(extResult.IsSuccess);
        Assert.Single(extResult.Data);
        Assert.Equal("ROLE_TARGET_3", extResult.Data[0].TargetId);

        // Filter by prefix-prepended code matches audit4
        var preResult = await sut.ExecuteAsync(new AuthorizationAuditQuery { PermissionCode = "prefix_assignments.assignments.create" });
        Assert.True(preResult.IsSuccess);
        Assert.Single(preResult.Data);
        Assert.Equal("ROLE_TARGET_4", preResult.Data[0].TargetId);
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

    [Fact]
    public async Task GetUserAuthorization_ActiveRolePermissionBreakdownUnion_EqualsAuthoritativePermissions()
    {
        await using var fixture = await CreateFixtureAsync();
        var now = DateTime.UtcNow;
        var centerId = fixture.TenantContext.CenterId!.Value;

        var teacher = new User
        {
            UserId = Guid.NewGuid(),
            CenterId = centerId,
            Username = "teacher-union-test",
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher Union Test",
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        fixture.Context.Users.Add(teacher);

        // Role 1: Active, has 2 active permissions and 1 deprecated/inactive permission
        var activePerm1 = await fixture.Context.Permissions.SingleAsync(p => p.PermissionCode == "knowledge.nodes.update");
        var activePerm2 = await fixture.Context.Permissions.SingleAsync(p => p.PermissionCode == "curriculum.curriculums.publish");
        var deprecatedPerm = new Permission
        {
            PermissionId = Guid.NewGuid(),
            PermissionCode = "deprecated.permission.test",
            ModuleName = "Test",
            ResourceName = "TestResource",
            ActionName = "Test",
            Description = "Deprecated test permission",
            Status = PermissionStatus.Deprecated,
            IsDelegable = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var deprecatedPermAccountType = new PermissionAccountType
        {
            PermissionId = deprecatedPerm.PermissionId,
            AccountType = UserRole.Teacher,
            CreatedAt = now
        };
        fixture.Context.Permissions.Add(deprecatedPerm);
        fixture.Context.PermissionAccountTypes.Add(deprecatedPermAccountType);

        var activeRole1 = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = centerId,
            RoleCode = "TEACHER_ROLE_ACTIVE_1",
            RoleName = "Active Role 1",
            AccountType = UserRole.Teacher,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        fixture.Context.AuthorizationRoles.Add(activeRole1);

        fixture.Context.RolePermissions.AddRange(
            new RolePermission
            {
                CenterId = centerId,
                RoleId = activeRole1.RoleId,
                PermissionId = activePerm1.PermissionId,
                AccountType = UserRole.Teacher,
                GrantedAt = now,
                GrantedByUserId = fixture.ManagerId
            },
            new RolePermission
            {
                CenterId = centerId,
                RoleId = activeRole1.RoleId,
                PermissionId = activePerm2.PermissionId,
                AccountType = UserRole.Teacher,
                GrantedAt = now,
                GrantedByUserId = fixture.ManagerId
            },
            new RolePermission
            {
                CenterId = centerId,
                RoleId = activeRole1.RoleId,
                PermissionId = deprecatedPerm.PermissionId,
                AccountType = UserRole.Teacher,
                GrantedAt = now,
                GrantedByUserId = fixture.ManagerId
            }
        );

        // Role 2: Active, has another active permission
        var activePerm3 = await fixture.Context.Permissions.SingleAsync(p => p.PermissionCode == "curriculum.questions.create");
        var activeRole2 = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = centerId,
            RoleCode = "TEACHER_ROLE_ACTIVE_2",
            RoleName = "Active Role 2",
            AccountType = UserRole.Teacher,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        fixture.Context.AuthorizationRoles.Add(activeRole2);
        fixture.Context.RolePermissions.Add(new RolePermission
        {
            CenterId = centerId,
            RoleId = activeRole2.RoleId,
            PermissionId = activePerm3.PermissionId,
            AccountType = UserRole.Teacher,
            GrantedAt = now,
            GrantedByUserId = fixture.ManagerId
        });

        // Role 3: Disabled/Archived role, has active permission
        var archivedRole = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = centerId,
            RoleCode = "TEACHER_ROLE_ARCHIVED",
            RoleName = "Archived Role",
            AccountType = UserRole.Teacher,
            Status = AuthorizationRoleStatus.Archived,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        fixture.Context.AuthorizationRoles.Add(archivedRole);
        fixture.Context.RolePermissions.Add(new RolePermission
        {
            CenterId = centerId,
            RoleId = archivedRole.RoleId,
            PermissionId = activePerm1.PermissionId,
            AccountType = UserRole.Teacher,
            GrantedAt = now,
            GrantedByUserId = fixture.ManagerId
        });

        // Assign Role 1, Role 2, Role 3 to teacher
        fixture.Context.UserRoleAssignments.AddRange(
            new UserRoleAssignment
            {
                CenterId = centerId,
                UserId = teacher.UserId,
                RoleId = activeRole1.RoleId,
                AccountType = UserRole.Teacher,
                Status = UserRoleAssignmentStatus.Active,
                AssignedAt = now,
                AssignedByUserId = fixture.ManagerId,
                RowVersion = 1
            },
            new UserRoleAssignment
            {
                CenterId = centerId,
                UserId = teacher.UserId,
                RoleId = activeRole2.RoleId,
                AccountType = UserRole.Teacher,
                Status = UserRoleAssignmentStatus.Active,
                AssignedAt = now,
                AssignedByUserId = fixture.ManagerId,
                RowVersion = 1
            },
            new UserRoleAssignment
            {
                CenterId = centerId,
                UserId = teacher.UserId,
                RoleId = archivedRole.RoleId,
                AccountType = UserRole.Teacher,
                Status = UserRoleAssignmentStatus.Active,
                AssignedAt = now,
                AssignedByUserId = fixture.ManagerId,
                RowVersion = 1
            }
        );
        await fixture.Context.SaveChangesAsync();

        var sut = new GetUserAuthorizationUseCase(
            fixture.Context,
            fixture.TenantContext,
            new AuthorizationSnapshotReader(fixture.Context));

        var result = await sut.ExecuteAsync(teacher.UserId);

        Assert.True(result.IsSuccess);
        var authDto = result.Data!;

        // Deprecated/Inactive permission is NOT in the authoritative Permissions collection
        Assert.DoesNotContain("deprecated.permission.test", authDto.Permissions);

        // Deprecated/Inactive permission is NOT in Role 1's PermissionCodes
        var role1Dto = authDto.Roles.Single(r => r.RoleId == activeRole1.RoleId);
        Assert.DoesNotContain("deprecated.permission.test", role1Dto.PermissionCodes);
        Assert.Equal(["curriculum.curriculums.publish", "knowledge.nodes.update"], role1Dto.PermissionCodes);

        // Role 2 has only its active permission
        var role2Dto = authDto.Roles.Single(r => r.RoleId == activeRole2.RoleId);
        Assert.Equal(["curriculum.questions.create"], role2Dto.PermissionCodes);

        // Archived role has empty PermissionCodes
        var archivedRoleDto = authDto.Roles.Single(r => r.RoleId == archivedRole.RoleId);
        Assert.Empty(archivedRoleDto.PermissionCodes);

        // The union of PermissionCodes from active role assignments exactly equals authoritative Permissions
        var activeRoleAssignments = authDto.Roles
            .Where(r => r.AssignmentStatus == nameof(UserRoleAssignmentStatus.Active));
        var unionOfActiveRolePermissions = activeRoleAssignments
            .SelectMany(r => r.PermissionCodes)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var authoritativePermissions = authDto.Permissions
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(authoritativePermissions, unionOfActiveRolePermissions);
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
