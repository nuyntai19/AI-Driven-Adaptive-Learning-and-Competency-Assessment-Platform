using System.Globalization;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

internal static class AuthorizationRoleProjection
{
    public static IQueryable<AuthorizationRoleDto> Project(
        this IQueryable<AuthorizationRole> query) => query.Select(role =>
        new AuthorizationRoleDto
        {
            RoleId = role.RoleId,
            RoleCode = role.RoleCode,
            RoleName = role.RoleName,
            AccountType = role.AccountType.ToString(),
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            Status = role.Status.ToString(),
            PermissionCodes = role.RolePermissions
                .OrderBy(mapping => mapping.PermissionAccountType.Permission.PermissionCode)
                .Select(mapping => mapping.PermissionAccountType.Permission.PermissionCode)
                .ToArray(),
            ActiveUserCount = role.UserAssignments.Count(assignment =>
                assignment.Status == UserRoleAssignmentStatus.Active),
            RowVersion = role.RowVersion.ToString(CultureInfo.InvariantCulture)
        });
}
