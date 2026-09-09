using EduTwin.Contracts.Common;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class GetAuthorizationRoleUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : IGetAuthorizationRoleUseCase
{
    public async Task<AuthorizationRoleResult> ExecuteAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved ||
            !tenantContext.CenterId.HasValue ||
            tenantContext.CenterId.Value == Guid.Empty ||
            !tenantContext.UserId.HasValue ||
            tenantContext.UserId.Value == Guid.Empty ||
            roleId == Guid.Empty)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var role = await dbContext.AuthorizationRoles
            .AsNoTracking()
            .Where(item => item.RoleId == roleId)
            .Project()
            .SingleOrDefaultAsync(cancellationToken);
        return role is null
            ? AuthorizationRoleResult.Failure(ErrorCodes.ResourceNotFound)
            : AuthorizationRoleResult.Success(role);
    }
}
