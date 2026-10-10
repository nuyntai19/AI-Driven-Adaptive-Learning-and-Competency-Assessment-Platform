using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.Organization;

internal static class AccountRoleTestSeed
{
    internal static AuthorizationRole Add(EduTwinDbContext db, Guid centerId, UserRole type, DateTime now)
    {
        var role = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(), CenterId = centerId, AccountType = type,
            RoleCode = $"SYSTEM_{type.ToString().ToUpperInvariant()}", RoleName = type.ToString(),
            IsSystemRole = true, Status = AuthorizationRoleStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        db.AuthorizationRoles.Add(role);
        return role;
    }
}
