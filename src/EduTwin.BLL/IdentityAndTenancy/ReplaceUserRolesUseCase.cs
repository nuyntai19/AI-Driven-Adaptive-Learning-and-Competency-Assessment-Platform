using System.Data;
using System.Globalization;
using System.Text.Json;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class ReplaceUserRolesUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext,
    IAuthorizationSnapshotReader snapshotReader,
    ITenantAdministratorGuard administratorGuard,
    TimeProvider timeProvider) : IReplaceUserRolesUseCase
{
    public async Task<UserAuthorizationResult> ExecuteAsync(
        Guid userId,
        ReplaceUserRolesRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActor(tenantContext, out var centerId, out var actorId) ||
            userId == Guid.Empty)
        {
            return UserAuthorizationResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var reason = request.Reason.Trim();
        if (!ulong.TryParse(request.RowVersion, NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion) ||
            expectedVersion == 0 ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 ||
            request.RoleIds.Any(id => id == Guid.Empty) ||
            request.RoleIds.Distinct().Count() != request.RoleIds.Count)
        {
            return UserAuthorizationResult.Failure(ErrorCodes.ValidationFailed);
        }

        var user = await dbContext.Users
            .Include(item => item.RoleAssignments)
                .ThenInclude(assignment => assignment.Role)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (user is null)
        {
            return UserAuthorizationResult.Failure(ErrorCodes.ResourceNotFound);
        }
        if (user.RowVersion != expectedVersion)
        {
            return UserAuthorizationResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var requestedRoleIdSet = request.RoleIds.ToHashSet();
        var roles = (await dbContext.AuthorizationRoles
                .AsNoTracking()
                .ToArrayAsync(cancellationToken))
            .Where(role => requestedRoleIdSet.Contains(role.RoleId))
            .ToArray();
        if (roles.Length != request.RoleIds.Count)
        {
            return UserAuthorizationResult.Failure(ErrorCodes.ResourceNotFound);
        }
        if (roles.Any(role => role.Status != AuthorizationRoleStatus.Active))
        {
            return UserAuthorizationResult.Failure(ErrorCodes.InvalidStateTransition);
        }
        if (roles.Any(role => role.AccountType != user.RoleName))
        {
            return UserAuthorizationResult.Failure(ErrorCodes.RoleAccountTypeMismatch);
        }

        if (user.RoleName == UserRole.CenterManager)
        {
            var selectedRoleIds = roles.Select(role => role.RoleId).ToHashSet();
            var selectedPermissions = (await dbContext.RolePermissions
                    .AsNoTracking()
                    .Include(mapping => mapping.PermissionAccountType)
                        .ThenInclude(mapping => mapping.Permission)
                    .ToArrayAsync(cancellationToken))
                .Where(mapping => selectedRoleIds.Contains(mapping.RoleId))
                .Select(mapping => mapping.PermissionAccountType.Permission.PermissionCode)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var actorSnapshot = await snapshotReader.ReadForUserAsync(
                actorId,
                cancellationToken);
            if (!selectedPermissions.ToHashSet(StringComparer.Ordinal)
                    .IsSubsetOf(actorSnapshot.Permissions))
            {
                return UserAuthorizationResult.Failure(ErrorCodes.AuthPrivilegeEscalation);
            }
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;
        if (user.RoleName == UserRole.CenterManager &&
            !await administratorGuard.HasAdministratorAfterAsync(
                changedUserId: userId,
                replacementUserRoleIds: request.RoleIds,
                cancellationToken: cancellationToken))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return UserAuthorizationResult.Failure(ErrorCodes.LastTenantAdmin);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var requestedRoleIds = requestedRoleIdSet;
        var beforeRoleIds = user.RoleAssignments
            .Where(assignment => assignment.Status == UserRoleAssignmentStatus.Active)
            .Select(assignment => assignment.RoleId)
            .Order()
            .ToArray();
        foreach (var assignment in user.RoleAssignments)
        {
            if (requestedRoleIds.Contains(assignment.RoleId))
            {
                if (assignment.Status == UserRoleAssignmentStatus.Revoked)
                {
                    assignment.Status = UserRoleAssignmentStatus.Active;
                    assignment.AssignedAt = now;
                    assignment.AssignedByUserId = actorId;
                    assignment.RevokedAt = null;
                    assignment.RevokedByUserId = null;
                    assignment.RevokeReason = null;
                    assignment.RowVersion++;
                }
            }
            else if (assignment.Status == UserRoleAssignmentStatus.Active)
            {
                assignment.Status = UserRoleAssignmentStatus.Revoked;
                assignment.RevokedAt = now;
                assignment.RevokedByUserId = actorId;
                assignment.RevokeReason = reason[..Math.Min(reason.Length, 500)];
                assignment.RowVersion++;
            }
        }

        var existingRoleIds = user.RoleAssignments
            .Select(assignment => assignment.RoleId)
            .ToHashSet();
        foreach (var roleId in requestedRoleIds.Except(existingRoleIds))
        {
            dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                CenterId = centerId,
                UserId = userId,
                RoleId = roleId,
                AccountType = user.RoleName,
                Status = UserRoleAssignmentStatus.Active,
                AssignedAt = now,
                AssignedByUserId = actorId,
                RowVersion = 1
            });
        }

        user.AuthVersion = checked(user.AuthVersion + 1);
        user.RowVersion++;
        user.UpdatedAt = now;
        user.UpdatedBy = actorId;
        dbContext.Entry(user).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokeReason = "Authorization roles changed";
        }

        dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = actorId,
            ActionType = "UserRolesReplaced",
            TargetType = "UserRole",
            TargetId = userId.ToString("D"),
            TargetUserId = userId,
            BeforeData = JsonSerializer.Serialize(new { RoleIds = beforeRoleIds }),
            AfterData = JsonSerializer.Serialize(new
            {
                RoleIds = request.RoleIds.Order().ToArray(),
                AuthorizationVersion = user.AuthVersion
            }),
            Reason = reason,
            TraceId = NormalizeTraceId(traceId),
            CreatedAt = now,
            CreatedBy = actorId
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return UserAuthorizationResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        return await BuildResponseAsync(user, cancellationToken);
    }

    private async Task<UserAuthorizationResult> BuildResponseAsync(
        EduTwin.DAL.IdentityAndTenancy.User user,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.UserRoleAssignments
            .AsNoTracking()
            .Where(assignment => assignment.UserId == user.UserId)
            .Include(assignment => assignment.Role)
            .OrderBy(assignment => assignment.Status)
            .ThenBy(assignment => assignment.Role.RoleCode)
            .ToArrayAsync(cancellationToken);
        var snapshot = await snapshotReader.ReadForUserAsync(
            user.UserId,
            cancellationToken);
        return UserAuthorizationResult.Success(new UserAuthorizationDto
        {
            UserId = user.UserId,
            AccountType = user.RoleName.ToString(),
            Roles = assignments.Select(assignment => new AssignedAuthorizationRoleDto
            {
                RoleId = assignment.RoleId,
                RoleCode = assignment.Role.RoleCode,
                RoleName = assignment.Role.RoleName,
                AccountType = assignment.AccountType.ToString(),
                AssignmentStatus = assignment.Status.ToString(),
                AssignedAt = assignment.AssignedAt,
                RevokedAt = assignment.RevokedAt
            }).ToArray(),
            Permissions = snapshot.Permissions,
            RowVersion = user.RowVersion.ToString(CultureInfo.InvariantCulture),
            AuthorizationVersion = user.AuthVersion
        });
    }

    private static bool TryResolveActor(
        ITenantContext context,
        out Guid centerId,
        out Guid actorId)
    {
        centerId = context.CenterId.GetValueOrDefault();
        actorId = context.UserId.GetValueOrDefault();
        return context.IsResolved && centerId != Guid.Empty && actorId != Guid.Empty;
    }

    private static string NormalizeTraceId(string traceId) =>
        string.IsNullOrWhiteSpace(traceId)
            ? "unknown"
            : traceId[..Math.Min(traceId.Length, 64)];
}
