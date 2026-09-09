using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class TenantAdministratorGuard(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : ITenantAdministratorGuard
{
    public async Task<bool> HasAdministratorAfterAsync(
        Guid? changedRoleId = null,
        bool? changedRoleActive = null,
        IReadOnlyCollection<Guid>? replacementPermissionIds = null,
        Guid? changedUserId = null,
        IReadOnlyCollection<Guid>? replacementUserRoleIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved ||
            !tenantContext.CenterId.HasValue ||
            tenantContext.CenterId.Value == Guid.Empty)
        {
            return false;
        }

        var administrators = await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.RoleName == UserRole.CenterManager &&
                user.Status == UserStatus.Active)
            .Select(user => user.UserId)
            .ToArrayAsync(cancellationToken);
        if (administrators.Length == 0)
        {
            return false;
        }

        var activeRoleIds = (await dbContext.AuthorizationRoles
                .AsNoTracking()
                .Where(role => role.Status == AuthorizationRoleStatus.Active)
                .Select(role => role.RoleId)
                .ToArrayAsync(cancellationToken))
            .ToHashSet();
        if (changedRoleId.HasValue && changedRoleActive.HasValue)
        {
            if (changedRoleActive.Value)
            {
                activeRoleIds.Add(changedRoleId.Value);
            }
            else
            {
                activeRoleIds.Remove(changedRoleId.Value);
            }
        }

        var administratorIds = administrators.ToHashSet();
        var assignments = (await dbContext.UserRoleAssignments
                .AsNoTracking()
                .Where(assignment =>
                    assignment.Status == UserRoleAssignmentStatus.Active)
                .Select(assignment => new { assignment.UserId, assignment.RoleId })
                .ToArrayAsync(cancellationToken))
            .Where(assignment => administratorIds.Contains(assignment.UserId))
            .ToArray();
        var permissions = (await dbContext.RolePermissions
                .AsNoTracking()
                .Select(mapping => new { mapping.RoleId, mapping.PermissionId })
                .ToArrayAsync(cancellationToken))
            .Where(mapping => activeRoleIds.Contains(mapping.RoleId))
            .ToArray();
        var corePermissionIds = (await dbContext.Permissions
                .AsNoTracking()
                .Select(permission => new
                {
                    permission.PermissionId,
                    permission.PermissionCode
                })
                .ToArrayAsync(cancellationToken))
            .Where(permission =>
                AuthorizationPermissionCatalog.TenantAdminCorePermissionsV1
                    .Contains(permission.PermissionCode))
            .Select(permission => permission.PermissionId)
            .ToHashSet();
        if (corePermissionIds.Count !=
            AuthorizationPermissionCatalog.TenantAdminCorePermissionsV1.Count)
        {
            return false;
        }

        foreach (var administratorId in administrators)
        {
            var roleIds = administratorId == changedUserId &&
                          replacementUserRoleIds is not null
                ? replacementUserRoleIds.ToHashSet()
                : assignments
                    .Where(assignment => assignment.UserId == administratorId)
                    .Select(assignment => assignment.RoleId)
                    .ToHashSet();
            roleIds.IntersectWith(activeRoleIds);

            var effectivePermissionIds = permissions
                .Where(mapping => roleIds.Contains(mapping.RoleId))
                .Select(mapping => mapping.PermissionId)
                .ToHashSet();
            if (changedRoleId.HasValue &&
                replacementPermissionIds is not null &&
                roleIds.Contains(changedRoleId.Value))
            {
                effectivePermissionIds.ExceptWith(permissions
                    .Where(mapping => mapping.RoleId == changedRoleId.Value)
                    .Select(mapping => mapping.PermissionId));
                effectivePermissionIds.UnionWith(replacementPermissionIds);
            }

            if (corePermissionIds.IsSubsetOf(effectivePermissionIds))
            {
                return true;
            }
        }

        return false;
    }
}
