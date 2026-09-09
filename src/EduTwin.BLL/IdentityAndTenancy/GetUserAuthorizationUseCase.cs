using System.Globalization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class GetUserAuthorizationUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext,
    IAuthorizationSnapshotReader snapshotReader) : IGetUserAuthorizationUseCase
{
    public async Task<UserAuthorizationResult> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsResolved(tenantContext) || userId == Guid.Empty)
        {
            return UserAuthorizationResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(item => item.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (user is null)
        {
            return UserAuthorizationResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var snapshot = await snapshotReader.ReadForUserAsync(userId, cancellationToken);
        return UserAuthorizationResult.Success(new UserAuthorizationDto
        {
            UserId = user.UserId,
            AccountType = user.RoleName.ToString(),
            Roles = user.RoleAssignments
                .OrderBy(assignment => assignment.Status)
                .ThenBy(assignment => assignment.Role.RoleCode, StringComparer.Ordinal)
                .Select(assignment => new AssignedAuthorizationRoleDto
                {
                    RoleId = assignment.RoleId,
                    RoleCode = assignment.Role.RoleCode,
                    RoleName = assignment.Role.RoleName,
                    AccountType = assignment.AccountType.ToString(),
                    AssignmentStatus = assignment.Status.ToString(),
                    AssignedAt = assignment.AssignedAt,
                    RevokedAt = assignment.RevokedAt
                })
                .ToArray(),
            Permissions = snapshot.Permissions,
            RowVersion = user.RowVersion.ToString(CultureInfo.InvariantCulture),
            AuthorizationVersion = user.AuthVersion
        });
    }

    private static bool IsResolved(ITenantContext context) =>
        context.IsResolved &&
        context.CenterId.HasValue && context.CenterId.Value != Guid.Empty &&
        context.UserId.HasValue && context.UserId.Value != Guid.Empty;
}
