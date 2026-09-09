using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class AuthorizationSnapshotReader(EduTwinDbContext dbContext)
    : IAuthorizationSnapshotReader
{
    public async Task<AuthorizationSnapshot> ReadForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return new AuthorizationSnapshot([], []);
        }

        var roles = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.UserId == userId &&
                assignment.Status == UserRoleAssignmentStatus.Active &&
                assignment.Role.Status == AuthorizationRoleStatus.Active)
            .Select(assignment => new AuthorizationRoleSummaryDto
            {
                RoleId = assignment.RoleId.ToString("D").ToLowerInvariant(),
                RoleCode = assignment.Role.RoleCode,
                RoleName = assignment.Role.RoleName,
                AccountType = assignment.AccountType.ToString()
            })
            .ToArrayAsync(cancellationToken);

        var orderedRoles = roles
            .OrderBy(role => role.RoleCode, StringComparer.Ordinal)
            .ThenBy(role => role.RoleId, StringComparer.Ordinal)
            .ToArray();
        if (orderedRoles.Length == 0)
        {
            return new AuthorizationSnapshot(orderedRoles, []);
        }

        var roleIds = orderedRoles
            .Select(role => Guid.Parse(role.RoleId))
            .ToArray();
        var permissionCodes = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(grant =>
                roleIds.Contains(grant.RoleId) &&
                grant.Role.Status == AuthorizationRoleStatus.Active &&
                grant.PermissionAccountType.Permission.Status == PermissionStatus.Active)
            .Select(grant => grant.PermissionAccountType.Permission.PermissionCode)
            .Distinct()
            .OrderBy(code => code)
            .ToArrayAsync(cancellationToken);

        return new AuthorizationSnapshot(orderedRoles, permissionCodes);
    }
}
