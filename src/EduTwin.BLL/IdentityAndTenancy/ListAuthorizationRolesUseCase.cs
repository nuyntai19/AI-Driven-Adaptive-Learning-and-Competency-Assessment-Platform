using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class ListAuthorizationRolesUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : IListAuthorizationRolesUseCase
{
    public async Task<ListAuthorizationRolesResult> ExecuteAsync(
        AuthorizationRoleListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!IsResolved(tenantContext))
        {
            return ListAuthorizationRolesResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            query.Search?.Length > 200 ||
            query.AccountType.HasValue && !Enum.IsDefined(query.AccountType.Value) ||
            query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            return ListAuthorizationRolesResult.Failure(ErrorCodes.ValidationFailed);
        }

        var search = query.Search?.Trim();
        var roles = dbContext.AuthorizationRoles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            roles = roles.Where(role =>
                role.RoleCode.Contains(search) ||
                role.RoleName.Contains(search));
        }
        if (query.AccountType.HasValue)
        {
            roles = roles.Where(role => role.AccountType == query.AccountType.Value);
        }
        if (query.Status.HasValue)
        {
            roles = roles.Where(role => role.Status == query.Status.Value);
        }

        var totalItems = await roles.LongCountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)query.PageSize);
        if ((query.Page - 1L) * query.PageSize >= totalItems)
        {
            return ListAuthorizationRolesResult.Success([], totalItems, totalPages);
        }

        var data = await roles
            .OrderBy(role => role.AccountType)
            .ThenBy(role => role.RoleName)
            .ThenBy(role => role.RoleId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Project()
            .ToArrayAsync(cancellationToken);
        return ListAuthorizationRolesResult.Success(data, totalItems, totalPages);
    }

    private static bool IsResolved(ITenantContext context) =>
        context.IsResolved &&
        context.CenterId.HasValue && context.CenterId.Value != Guid.Empty &&
        context.UserId.HasValue && context.UserId.Value != Guid.Empty;
}
