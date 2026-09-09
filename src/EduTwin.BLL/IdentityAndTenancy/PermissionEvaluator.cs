using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class PermissionEvaluator(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : IPermissionEvaluator
{
    public async Task<bool> HasPermissionAsync(
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode) ||
            !tenantContext.IsResolved ||
            !tenantContext.CenterId.HasValue ||
            tenantContext.CenterId.Value == Guid.Empty ||
            !tenantContext.UserId.HasValue ||
            tenantContext.UserId.Value == Guid.Empty)
        {
            return false;
        }

        return await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.UserId == tenantContext.UserId!.Value &&
                assignment.Status == UserRoleAssignmentStatus.Active &&
                assignment.Role.Status == AuthorizationRoleStatus.Active)
            .AnyAsync(
                assignment => assignment.Role.RolePermissions.Any(grant =>
                    grant.PermissionAccountType.Permission.PermissionCode == permissionCode &&
                    grant.PermissionAccountType.Permission.Status == PermissionStatus.Active),
                cancellationToken);
    }
}
