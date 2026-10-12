using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Seeding;

public sealed class AuthorizationBootstrapper(
    EduTwinDbContext dbContext,
    TimeProvider timeProvider)
{
    public static readonly Guid ReservedPlatformCenterId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        var centers = await dbContext.Centers
            .IgnoreQueryFilters()
            .Where(center => !center.IsDeleted && center.CenterId != ReservedPlatformCenterId)
            .Select(center => center.CenterId)
            .ToArrayAsync(cancellationToken);

        var permissions = await dbContext.PermissionAccountTypes
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        foreach (var centerId in centers)
        {
            await EnsureCenterAsync(centerId, permissions, cancellationToken);
        }

        await ReconcileGlobalPermissionAccountTypesAsync(cancellationToken);
    }

    public async Task EnsureCenterAsync(
        Guid centerId,
        IReadOnlyCollection<PermissionAccountType>? permissionMappings = null,
        CancellationToken cancellationToken = default)
    {
        var changed = false;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // 1. Reconcile base permissions from catalog
        var catalogPermissions = AuthorizationPermissionCatalog.CreatePermissions();
        var existingPermissions = await dbContext.Permissions
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.PermissionCode, cancellationToken);

        foreach (var perm in catalogPermissions)
        {
            if (!existingPermissions.TryGetValue(perm.PermissionCode, out var existing))
            {
                dbContext.Permissions.Add(perm);
                existingPermissions.Add(perm.PermissionCode, perm);
                changed = true;
            }
        }

        // 2. Reconcile PermissionAccountTypes from catalog
        var catalogMappings = AuthorizationPermissionCatalog.CreateAccountTypeMappings();
        var existingMappings = await dbContext.PermissionAccountTypes
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        var existingMappingKeys = existingMappings
            .Select(m => (m.PermissionId, m.AccountType))
            .ToHashSet();

        foreach (var mapping in catalogMappings)
        {
            if (existingMappingKeys.Add((mapping.PermissionId, mapping.AccountType)))
            {
                dbContext.PermissionAccountTypes.Add(mapping);
                existingMappings.Add(mapping);
                changed = true;
            }
        }



        var users = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => user.CenterId == centerId && !user.IsDeleted)
            .ToArrayAsync(cancellationToken);
        var actor = users
            .Where(user => user.RoleName == UserRole.CenterManager &&
                           user.Status == UserStatus.Active)
            .OrderBy(user => user.UserId)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Center {centerId:D} has no active CenterManager for authorization bootstrap.");

        var roles = await dbContext.AuthorizationRoles
            .IgnoreQueryFilters()
            .Where(role => role.CenterId == centerId)
            .Include(r => r.RolePermissions)
            .ToListAsync(cancellationToken);

        var usersToBumpAuthVersion = new HashSet<Guid>();
        var assignments = await dbContext.UserRoleAssignments
            .IgnoreQueryFilters()
            .Where(item => item.CenterId == centerId)
            .ToListAsync(cancellationToken);

        void InvalidateRoleSessions(AuthorizationRole changedRole)
        {
            if (changedRole.Status != AuthorizationRoleStatus.Active || changedRole.IsDeleted)
                return;
            foreach (var assignment in assignments.Where(item =>
                         item.RoleId == changedRole.RoleId && item.Status == UserRoleAssignmentStatus.Active))
                usersToBumpAuthVersion.Add(assignment.UserId);
        }

        // 3. Reconcile System Roles using explicit default permission sets
        foreach (var accountType in Enum.GetValues<UserRole>())
        {
            if (accountType == UserRole.PlatformAdmin)
            {
                continue;
            }

            var role = roles.SingleOrDefault(item => item.IsSystemRole && item.AccountType == accountType);
            if (role is null)
            {
                role = CreateSystemRole(centerId, accountType, utcNow);
                dbContext.AuthorizationRoles.Add(role);
                roles.Add(role);
                changed = true;
            }

            if (role.IsDeleted || role.Status != AuthorizationRoleStatus.Active)
                continue; // An archived system role is a deliberate administrative decision.

            var defaultCodes = AuthorizationPermissionCatalog.GetDefaultSystemRoleCodes(accountType);
            var targetPermissionIds = defaultCodes
                .Where(code => existingPermissions.ContainsKey(code))
                .Select(code => existingPermissions[code].PermissionId)
                .ToHashSet();

            var currentRolePermissions = role.RolePermissions.ToList();
            var roleChanged = false;

            // Revoke permissions no longer in the explicit default set for this system role
            foreach (var rp in currentRolePermissions)
            {
                if (!targetPermissionIds.Contains(rp.PermissionId))
                {
                    dbContext.RolePermissions.Remove(rp);
                    changed = true;
                    roleChanged = true;
                }
            }

            var currentPermIds = currentRolePermissions
                .Where(rp => targetPermissionIds.Contains(rp.PermissionId))
                .Select(rp => rp.PermissionId)
                .ToHashSet();

            // Grant missing default permissions
            foreach (var permId in targetPermissionIds)
            {
                if (!currentPermIds.Contains(permId))
                {
                    dbContext.RolePermissions.Add(new RolePermission
                    {
                        CenterId = centerId,
                        RoleId = role.RoleId,
                        PermissionId = permId,
                        AccountType = accountType,
                        GrantedAt = utcNow,
                        GrantedByUserId = actor.UserId
                    });
                    changed = true;
                    roleChanged = true;
                }
            }

            if (roleChanged)
            {
                role.UpdatedAt = utcNow; // Also advances RowVersion for existing roles.
                if (dbContext.Entry(role).State != EntityState.Added)
                    dbContext.Entry(role).Property(item => item.UpdatedAt).IsModified = true;
                InvalidateRoleSessions(role); // Grants and revocations both refresh the UI snapshot.
            }
        }

        // 4. Reconcile Custom Roles: purge permissions no longer compatible with account type
        var customRoles = roles.Where(r => !r.IsSystemRole).ToList();
        foreach (var customRole in customRoles)
        {
            var validCompatibleIds = catalogMappings
                .Where(m => m.AccountType == customRole.AccountType)
                .Select(m => m.PermissionId)
                .ToHashSet();

            foreach (var rp in customRole.RolePermissions.ToList())
            {
                if (!validCompatibleIds.Contains(rp.PermissionId))
                {
                    dbContext.RolePermissions.Remove(rp);
                    changed = true;
                    customRole.UpdatedAt = utcNow;
                    InvalidateRoleSessions(customRole);
                }
            }
        }

        // 5. Invalidate existing sessions for affected users
        if (usersToBumpAuthVersion.Count > 0)
        {
            var affectedUsers = users.Where(u => usersToBumpAuthVersion.Contains(u.UserId)).ToList();
            foreach (var u in affectedUsers)
            {
                u.AuthVersion = checked(u.AuthVersion + 1);
                u.UpdatedAt = utcNow;
            }
            changed = true;
        }

        // 6. Ensure default system role assignment for each user
        var assignmentKeys = assignments
            .Select(item => (item.UserId, item.RoleId))
            .ToHashSet();
        foreach (var user in users)
        {
            if (user.RoleName == UserRole.PlatformAdmin)
            {
                continue;
            }

            var role = roles.Single(item => item.IsSystemRole && item.AccountType == user.RoleName);
            if (role.IsDeleted || role.Status != AuthorizationRoleStatus.Active)
                continue;
            if (assignmentKeys.Add((user.UserId, role.RoleId)))
            {
                dbContext.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    CenterId = centerId,
                    UserId = user.UserId,
                    RoleId = role.RoleId,
                    AccountType = user.RoleName,
                    Status = UserRoleAssignmentStatus.Active,
                    AssignedAt = utcNow,
                    AssignedByUserId = actor.UserId
                });
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = null,
            ActionType = "AuthorizationBootstrap",
            TargetType = "Center",
            TargetId = centerId.ToString("D"),
            AfterData = "{\"catalogVersion\":\"v2\",\"reconciliation\":\"role-boundaries-separated\"}",
            Reason = "Khởi tạo hoặc đối soát phân quyền động theo ranh giới vai trò chuẩn hóa.",
            TraceId = "runtime-seed:authorization-bootstrap",
            CreatedAt = utcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReconcileGlobalPermissionAccountTypesAsync(CancellationToken cancellationToken)
    {
        var catalogMappings = AuthorizationPermissionCatalog.CreateAccountTypeMappings();
        var catalogMappingKeys = catalogMappings
            .Select(m => (m.PermissionId, m.AccountType))
            .ToHashSet();

        var existingMappings = await dbContext.PermissionAccountTypes
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var obsoleteMappings = existingMappings
            .Where(m => !catalogMappingKeys.Contains((m.PermissionId, m.AccountType)))
            .ToList();

        if (obsoleteMappings.Count > 0)
        {
            var obsoleteKeys = obsoleteMappings
                .Select(m => (m.PermissionId, m.AccountType))
                .ToHashSet();

            var allRolePermissions = await dbContext.RolePermissions
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken);

            var orphanedRolePermissions = allRolePermissions
                .Where(rp => obsoleteKeys.Contains((rp.PermissionId, rp.AccountType)))
                .ToList();

            if (orphanedRolePermissions.Count > 0)
            {
                dbContext.RolePermissions.RemoveRange(orphanedRolePermissions);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            dbContext.PermissionAccountTypes.RemoveRange(obsoleteMappings);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task BootstrapPlatformAsync(
        Guid platformCenterId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var role = await dbContext.AuthorizationRoles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.CenterId == platformCenterId && r.AccountType == UserRole.PlatformAdmin && r.IsSystemRole, cancellationToken);

        if (role is null)
        {
            role = CreateSystemRole(platformCenterId, UserRole.PlatformAdmin, utcNow);
            dbContext.AuthorizationRoles.Add(role);
        }

        var platformPermissions = await dbContext.PermissionAccountTypes
            .IgnoreQueryFilters()
            .Where(p => p.AccountType == UserRole.PlatformAdmin)
            .Select(p => p.PermissionId)
            .ToArrayAsync(cancellationToken);

        var existingRolePermissions = await dbContext.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.CenterId == platformCenterId && rp.RoleId == role.RoleId)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync(cancellationToken);

        var grantedCodes = new List<Guid>();
        foreach (var permId in platformPermissions)
        {
            if (existingRolePermissions.Add(permId))
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    CenterId = platformCenterId,
                    RoleId = role.RoleId,
                    PermissionId = permId,
                    AccountType = UserRole.PlatformAdmin,
                    GrantedAt = utcNow,
                    GrantedByUserId = adminUserId
                });
                grantedCodes.Add(permId);
            }
        }

        if (grantedCodes.Count > 0)
        {
            role.UpdatedAt = utcNow;
            if (dbContext.Entry(role).State != EntityState.Added)
                dbContext.Entry(role).Property(item => item.UpdatedAt).IsModified = true;
            if (role.Status == AuthorizationRoleStatus.Active && !role.IsDeleted)
            {
                var affectedUsers = await dbContext.Users.IgnoreQueryFilters()
                    .Where(user => user.CenterId == platformCenterId && !user.IsDeleted &&
                        dbContext.UserRoleAssignments.IgnoreQueryFilters().Any(assignment =>
                            assignment.CenterId == platformCenterId && assignment.UserId == user.UserId &&
                            assignment.RoleId == role.RoleId && assignment.Status == UserRoleAssignmentStatus.Active))
                    .ToListAsync(cancellationToken);
                foreach (var user in affectedUsers)
                {
                    user.AuthVersion = checked(user.AuthVersion + 1);
                    user.UpdatedAt = utcNow;
                }
            }
            dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
            {
                CenterId = platformCenterId, ActorUserId = null,
                ActionType = "PlatformDefaultPermissionsGranted", TargetType = "Role",
                TargetId = role.RoleId.ToString("D"),
                AfterData = System.Text.Json.JsonSerializer.Serialize(new { GrantedPermissionIds = grantedCodes }),
                Reason = "Bổ sung quyền mặc định còn thiếu cho vai trò quản trị nền tảng.",
                TraceId = "runtime-seed:platform-permissions", CreatedAt = utcNow
            });
        }

        var assignmentExists = await dbContext.UserRoleAssignments
            .IgnoreQueryFilters()
            .AnyAsync(a => a.CenterId == platformCenterId && a.UserId == adminUserId && a.RoleId == role.RoleId, cancellationToken);

        if (!assignmentExists)
        {
            dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                CenterId = platformCenterId,
                UserId = adminUserId,
                RoleId = role.RoleId,
                AccountType = UserRole.PlatformAdmin,
                Status = UserRoleAssignmentStatus.Active,
                AssignedAt = utcNow,
                AssignedByUserId = adminUserId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuthorizationRole CreateSystemRole(
        Guid centerId,
        UserRole accountType,
        DateTime utcNow)
    {
        var roleName = accountType switch
        {
            UserRole.Student => "Học viên hệ thống",
            UserRole.Teacher => "Giáo viên hệ thống",
            UserRole.CenterManager => "Quản trị trung tâm",
            UserRole.PlatformAdmin => "Quản trị viên nền tảng",
            _ => throw new ArgumentOutOfRangeException(nameof(accountType))
        };

        return new AuthorizationRole
        {
            RoleId = AuthorizationPermissionCatalog.CreateSystemRoleId(centerId, accountType),
            RoleCode = $"SYSTEM_{accountType.ToString().ToUpperInvariant()}",
            RoleName = roleName,
            AccountType = accountType,
            Description = "Vai trò hệ thống được tạo khi chuyển đổi sang phân quyền động.",
            IsSystemRole = true,
            Status = AuthorizationRoleStatus.Active,
            CenterId = centerId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }
}
