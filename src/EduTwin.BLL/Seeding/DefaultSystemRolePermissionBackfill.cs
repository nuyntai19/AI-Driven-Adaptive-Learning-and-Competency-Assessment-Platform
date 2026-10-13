using System.Data;
using System.Text.Json;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Seeding;

// Startup-only, additive reconciliation. Works when demo seeding is disabled.
// Never creates/reactivates a role assignment, edits a custom role, or grants an
// opt-in permission merely because it is compatible with an account type.
public sealed class DefaultSystemRolePermissionBackfill(EduTwinDbContext db, TimeProvider clock)
{
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var roles = await db.AuthorizationRoles.IgnoreQueryFilters()
            .Where(r => r.IsSystemRole && !r.IsDeleted && r.Status == AuthorizationRoleStatus.Active &&
                !r.Center.IsDeleted && r.Center.Status == CenterStatus.Active)
            .Include(r => r.RolePermissions).ToListAsync(cancellationToken);
        var permissions = await db.Permissions.AsNoTracking()
            .Where(p => p.Status == PermissionStatus.Active).ToDictionaryAsync(p => p.PermissionCode, cancellationToken);
        var compatible = (await db.PermissionAccountTypes.AsNoTracking().ToArrayAsync(cancellationToken))
            .Select(m => (m.AccountType, m.PermissionId)).ToHashSet();
        // The grant row requires a real, same-center administrator FK. The audit
        // actor remains null because startup, not that human, performed the action.
        var grantors = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => !u.IsDeleted && u.Status == UserStatus.Active &&
                (u.RoleName == UserRole.CenterManager || u.RoleName == UserRole.PlatformAdmin))
            .OrderBy(u => u.UserId).Select(u => new { u.CenterId, u.UserId, u.RoleName })
            .ToArrayAsync(cancellationToken);
        var changedRoleIds = new HashSet<Guid>();
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var role in roles)
        {
            var currentIds = role.RolePermissions.Select(p => p.PermissionId).ToHashSet();
            var missing = AuthorizationPermissionCatalog.GetDefaultSystemRoleCodes(role.AccountType)
                .Where(code => permissions.TryGetValue(code, out var p) &&
                    compatible.Contains((role.AccountType, p.PermissionId)) && !currentIds.Contains(p.PermissionId))
                .Order(StringComparer.Ordinal).ToArray();
            if (missing.Length == 0) continue;
            var grantor = grantors.FirstOrDefault(u => u.CenterId == role.CenterId &&
                u.RoleName == (role.AccountType == UserRole.PlatformAdmin ? UserRole.PlatformAdmin : UserRole.CenterManager));
            if (grantor is null) continue; // Never invent a grantor or use an administrator from another center.

            foreach (var code in missing)
                db.RolePermissions.Add(new RolePermission
                {
                    CenterId = role.CenterId, RoleId = role.RoleId, AccountType = role.AccountType,
                    PermissionId = permissions[code].PermissionId, GrantedAt = now, GrantedByUserId = grantor.UserId
                });
            role.UpdatedAt = now;
            db.Entry(role).Property(r => r.UpdatedAt).IsModified = true;
            changedRoleIds.Add(role.RoleId);
            db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
            {
                CenterId = role.CenterId, ActorUserId = null,
                ActionType = "SystemRoleDefaultPermissionsGranted", TargetType = "Role",
                TargetId = role.RoleId.ToString("D"),
                BeforeData = JsonSerializer.Serialize(new { PermissionCodes = permissions.Values
                    .Where(p => currentIds.Contains(p.PermissionId)).Select(p => p.PermissionCode).Order().ToArray() }),
                AfterData = JsonSerializer.Serialize(new { role.RoleCode, GrantedPermissionCodes = missing }),
                Reason = "Bổ sung quyền mặc định còn thiếu đúng loại tài khoản; giữ nguyên vai trò riêng và các phân quyền đã thu hồi.",
                TraceId = "startup:default-role-permissions", CreatedAt = now
            });
        }

        if (changedRoleIds.Count > 0)
        {
            // This MySQL provider cannot bind a primitive Guid collection for
            // Contains(). Use scalar role IDs, keeping the user filter in SQL.
            var affectedUsers = new Dictionary<Guid, User>();
            foreach (var changedRoleId in changedRoleIds)
            {
                var users = await db.Users.IgnoreQueryFilters()
                    .Where(u => !u.IsDeleted && db.UserRoleAssignments.IgnoreQueryFilters().Any(a =>
                        a.UserId == u.UserId && a.CenterId == u.CenterId && a.RoleId == changedRoleId &&
                        a.Status == UserRoleAssignmentStatus.Active))
                    .ToListAsync(cancellationToken);
                foreach (var user in users) affectedUsers.TryAdd(user.UserId, user);
            }
            foreach (var user in affectedUsers.Values)
            {
                user.AuthVersion = checked(user.AuthVersion + 1);
                user.UpdatedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }
}
