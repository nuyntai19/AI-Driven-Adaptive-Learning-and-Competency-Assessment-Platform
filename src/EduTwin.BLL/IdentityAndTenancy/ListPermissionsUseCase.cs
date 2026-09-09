using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class ListPermissionsUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : IListPermissionsUseCase
{
    public async Task<ListPermissionsResult> ExecuteAsync(
        PermissionListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved ||
            !tenantContext.CenterId.HasValue ||
            tenantContext.CenterId.Value == Guid.Empty ||
            !tenantContext.UserId.HasValue ||
            tenantContext.UserId.Value == Guid.Empty)
        {
            return ListPermissionsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (query.Page < 1 ||
            query.PageSize is < 1 or > 100 ||
            query.Module?.Length > 64 ||
            query.AccountType.HasValue &&
            !Enum.IsDefined(query.AccountType.Value) ||
            query.Status.HasValue &&
            !Enum.IsDefined(query.Status.Value))
        {
            return ListPermissionsResult.Failure(ErrorCodes.ValidationFailed);
        }

        var module = query.Module?.Trim();
        if (string.IsNullOrWhiteSpace(module))
        {
            module = null;
        }

        var permissions = dbContext.Permissions
            .AsNoTracking()
            .AsQueryable();
        if (module is not null)
        {
            permissions = permissions.Where(permission =>
                permission.ModuleName == module);
        }

        if (query.AccountType.HasValue)
        {
            permissions = permissions.Where(permission =>
                permission.AllowedAccountTypes.Any(mapping =>
                    mapping.AccountType == query.AccountType.Value));
        }

        if (query.Status.HasValue)
        {
            permissions = permissions.Where(permission =>
                permission.Status == query.Status.Value);
        }

        var totalItems = await permissions.LongCountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)query.PageSize);
        if ((query.Page - 1L) * query.PageSize >= totalItems)
        {
            return ListPermissionsResult.Success([], totalItems, totalPages);
        }

        var page = await permissions
            .Include(permission => permission.AllowedAccountTypes)
            .OrderBy(permission => permission.ModuleName)
            .ThenBy(permission => permission.ResourceName)
            .ThenBy(permission => permission.ActionName)
            .ThenBy(permission => permission.PermissionCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var data = page.Select(permission => new PermissionDto
        {
            PermissionCode = permission.PermissionCode,
            Module = permission.ModuleName,
            Resource = permission.ResourceName,
            Action = permission.ActionName,
            Description = permission.Description,
            AllowedAccountTypes = permission.AllowedAccountTypes
                .OrderBy(mapping => mapping.AccountType)
                .Select(mapping => mapping.AccountType.ToString())
                .ToArray(),
            IsSensitive = permission.IsSensitive,
            IsDelegable = permission.IsDelegable,
            Status = permission.Status.ToString()
        }).ToArray();

        return ListPermissionsResult.Success(data, totalItems, totalPages);
    }
}
