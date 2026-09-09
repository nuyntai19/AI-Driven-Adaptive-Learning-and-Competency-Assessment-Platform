using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public class GetCurrentUserUseCase : IGetCurrentUserUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IAuthorizationSnapshotReader _authorizationSnapshotReader;

    public GetCurrentUserUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IAuthorizationSnapshotReader authorizationSnapshotReader)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _authorizationSnapshotReader = authorizationSnapshotReader;
    }

    public async Task<GetCurrentUserResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return new GetCurrentUserResult { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };
        }

        var role = _tenantContext.Role;
        if (role != nameof(UserRole.Student) &&
            role != nameof(UserRole.Teacher) &&
            role != nameof(UserRole.CenterManager))
        {
            return new GetCurrentUserResult { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };
        }

        var userId = _tenantContext.UserId.Value;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.DisplayName,
                u.RoleName,
                u.Status,
                u.AuthVersion,
                CenterId = u.Center != null ? u.Center.CenterId : Guid.Empty,
                CenterName = u.Center != null ? u.Center.CenterName : string.Empty,
                CenterStatus = u.Center != null ? u.Center.Status : EduTwin.Contracts.Organization.CenterStatus.Suspended,
                CenterIsDeleted = u.Center != null ? u.Center.IsDeleted : true
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null || user.CenterIsDeleted || user.CenterId == Guid.Empty)
        {
            return new GetCurrentUserResult { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };
        }

        if (user.CenterStatus == EduTwin.Contracts.Organization.CenterStatus.Suspended)
        {
            return new GetCurrentUserResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthUserDisabled };
        }

        if (user.Status == UserStatus.Locked || user.Status == UserStatus.Disabled)
        {
            return new GetCurrentUserResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthUserDisabled };
        }

        var authorization = await _authorizationSnapshotReader.ReadForUserAsync(
            user.UserId,
            cancellationToken);

        return new GetCurrentUserResult
        {
            IsSuccess = true,
            Data = new CurrentUserDataDto
            {
                UserId = user.UserId.ToString("D").ToLowerInvariant(),
                CenterId = user.CenterId.ToString("D").ToLowerInvariant(),
                CenterName = user.CenterName,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AccountType = user.RoleName.ToString(),
                Role = user.RoleName.ToString(),
                Status = user.Status.ToString(),
                Roles = authorization.Roles,
                Permissions = authorization.Permissions,
                AuthorizationVersion = user.AuthVersion
            }
        };
    }
}
