using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class ListAuthorizationUsersUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : IListAuthorizationUsersUseCase
{
    public async Task<ListAuthorizationUsersResult> ExecuteAsync(
        AuthorizationUserListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!IsResolved(tenantContext))
        {
            return ListAuthorizationUsersResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            query.Search?.Length > 200 ||
            query.AccountType.HasValue && !Enum.IsDefined(query.AccountType.Value) ||
            query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            return ListAuthorizationUsersResult.Failure(ErrorCodes.ValidationFailed);
        }

        var search = query.Search?.Trim();
        var users = dbContext.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users.Where(user =>
                user.Username.Contains(search) ||
                user.DisplayName.Contains(search));
        }
        if (query.AccountType.HasValue)
        {
            users = users.Where(user => user.RoleName == query.AccountType.Value);
        }
        if (query.Status.HasValue)
        {
            users = users.Where(user => user.Status == query.Status.Value);
        }

        var totalItems = await users.LongCountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)query.PageSize);
        if ((query.Page - 1L) * query.PageSize >= totalItems)
        {
            return ListAuthorizationUsersResult.Success([], totalItems, totalPages);
        }

        var rows = await users
            .OrderBy(user => user.RoleName)
            .ThenBy(user => user.DisplayName)
            .ThenBy(user => user.UserId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(user => new AuthorizationUserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AccountType = user.RoleName,
                Status = user.Status,
                RowVersion = user.RowVersion.ToString(),
                AuthVersion = user.AuthVersion,
                CreatedAt = user.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        return ListAuthorizationUsersResult.Success(rows, totalItems, totalPages);
    }

    private static bool IsResolved(ITenantContext context) =>
        context.IsResolved &&
        context.CenterId.HasValue && context.CenterId.Value != Guid.Empty &&
        context.UserId.HasValue && context.UserId.Value != Guid.Empty;
}
