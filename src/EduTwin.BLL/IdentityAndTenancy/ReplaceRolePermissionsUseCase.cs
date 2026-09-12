using System.Data;
using System.Globalization;
using System.Text.Json;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class ReplaceRolePermissionsUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext,
    IAuthorizationSnapshotReader snapshotReader,
    ITenantAdministratorGuard administratorGuard,
    TimeProvider timeProvider) : IReplaceRolePermissionsUseCase
{
    public async Task<AuthorizationRoleResult> ExecuteAsync(
        Guid roleId,
        ReplaceRolePermissionsRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActor(tenantContext, out var centerId, out var actorId) ||
            roleId == Guid.Empty)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var reason = request.Reason.Trim();
        var codes = request.PermissionCodes
            .Select(code => code?.Trim() ?? string.Empty)
            .ToArray();
        if (!ulong.TryParse(request.RowVersion, NumberStyles.None,
                CultureInfo.InvariantCulture, out var expectedVersion) ||
            expectedVersion == 0 ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 1000 ||
            codes.Any(code => string.IsNullOrWhiteSpace(code) || code.Length > 100) ||
            codes.Distinct(StringComparer.Ordinal).Count() != codes.Length)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ValidationFailed);
        }

        var role = await dbContext.AuthorizationRoles
            .Include(item => item.RolePermissions)
                .ThenInclude(mapping => mapping.PermissionAccountType)
                    .ThenInclude(mapping => mapping.Permission)
            .Include(item => item.UserAssignments)
            .SingleOrDefaultAsync(item => item.RoleId == roleId, cancellationToken);
        if (role is null)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ResourceNotFound);
        }
        if (role.Status != AuthorizationRoleStatus.Active)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.InvalidStateTransition);
        }
        if (role.RowVersion != expectedVersion)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        if (role.AccountType == UserRole.PlatformAdmin ||
            codes.Any(code => code.StartsWith("platform.", StringComparison.OrdinalIgnoreCase)))
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.AuthPrivilegeEscalation);
        }

        var requestedCodes = codes.ToHashSet(StringComparer.Ordinal);
        var permissions = (await dbContext.Permissions
                .AsNoTracking()
                .ToArrayAsync(cancellationToken))
            .Where(permission => requestedCodes.Contains(permission.PermissionCode))
            .ToArray();
        if (permissions.Length != codes.Length ||
            permissions.Any(permission =>
                permission.Status != PermissionStatus.Active ||
                !permission.IsDelegable))
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ValidationFailed);
        }

        var permissionIds = permissions.Select(permission => permission.PermissionId).ToArray();
        var permissionIdSet = permissionIds.ToHashSet();
        var compatibleIds = (await dbContext.PermissionAccountTypes
                .AsNoTracking()
                .Where(mapping => mapping.AccountType == role.AccountType)
                .Select(mapping => mapping.PermissionId)
                .ToArrayAsync(cancellationToken))
            .Where(permissionIdSet.Contains)
            .ToArray();
        if (compatibleIds.Length != permissionIds.Length)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.RoleAccountTypeMismatch);
        }

        if (role.AccountType == UserRole.CenterManager)
        {
            var actorSnapshot = await snapshotReader.ReadForUserAsync(
                actorId,
                cancellationToken);
            if (!codes.ToHashSet(StringComparer.Ordinal)
                    .IsSubsetOf(actorSnapshot.Permissions))
            {
                return AuthorizationRoleResult.Failure(ErrorCodes.AuthPrivilegeEscalation);
            }
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;
        if (role.AccountType == UserRole.CenterManager &&
            !await administratorGuard.HasAdministratorAfterAsync(
                changedRoleId: roleId,
                changedRoleActive: true,
                replacementPermissionIds: permissionIds,
                cancellationToken: cancellationToken))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return AuthorizationRoleResult.Failure(ErrorCodes.LastTenantAdmin);
        }

        var beforeCodes = role.RolePermissions
            .Select(mapping => mapping.PermissionAccountType.Permission.PermissionCode)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var affectedUsers = await dbContext.Users
            .Where(user => user.RoleAssignments.Any(assignment =>
                assignment.RoleId == roleId &&
                assignment.Status == UserRoleAssignmentStatus.Active))
            .ToArrayAsync(cancellationToken);
        var affectedUserIds = affectedUsers.Select(user => user.UserId).ToHashSet();
        var tokens = (await dbContext.RefreshTokens
                .Where(token => token.RevokedAt == null)
                .ToArrayAsync(cancellationToken))
            .Where(token => affectedUserIds.Contains(token.UserId))
            .ToArray();

        var requestedPermissionIds = permissionIds.ToHashSet();
        var existingPermissionIds = role.RolePermissions
            .Select(mapping => mapping.PermissionId)
            .ToHashSet();
        dbContext.RolePermissions.RemoveRange(role.RolePermissions.Where(mapping =>
            !requestedPermissionIds.Contains(mapping.PermissionId)));
        dbContext.RolePermissions.AddRange(requestedPermissionIds
            .Except(existingPermissionIds)
            .Select(permissionId =>
            new RolePermission
            {
                CenterId = centerId,
                RoleId = roleId,
                PermissionId = permissionId,
                AccountType = role.AccountType,
                GrantedAt = now,
                GrantedByUserId = actorId
            }));
        role.RowVersion++;
        role.UpdatedAt = now;
        role.UpdatedBy = actorId;
        dbContext.Entry(role).Property(item => item.RowVersion).OriginalValue = expectedVersion;
        foreach (var user in affectedUsers)
        {
            user.AuthVersion = checked(user.AuthVersion + 1);
            user.RowVersion++;
            user.UpdatedAt = now;
            user.UpdatedBy = actorId;
        }
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
            token.RevokeReason = "Authorization permissions changed";
        }

        dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = actorId,
            ActionType = "RolePermissionsReplaced",
            TargetType = "RolePermission",
            TargetId = roleId.ToString("D"),
            BeforeData = JsonSerializer.Serialize(new { PermissionCodes = beforeCodes }),
            AfterData = JsonSerializer.Serialize(new
            {
                PermissionCodes = codes.Order(StringComparer.Ordinal).ToArray()
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
            return AuthorizationRoleResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        return AuthorizationRoleResult.Success(new AuthorizationRoleDto
        {
            RoleId = role.RoleId,
            RoleCode = role.RoleCode,
            RoleName = role.RoleName,
            AccountType = role.AccountType.ToString(),
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            Status = role.Status.ToString(),
            PermissionCodes = codes.Order(StringComparer.Ordinal).ToArray(),
            ActiveUserCount = affectedUsers.Length,
            RowVersion = role.RowVersion.ToString(CultureInfo.InvariantCulture)
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
