using System.Data;
using System.Globalization;
using System.Text.Json;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class UpdateAuthorizationRoleUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAdministratorGuard administratorGuard,
    IPermissionEvaluator permissionEvaluator,
    TimeProvider timeProvider) : IUpdateAuthorizationRoleUseCase
{
    public async Task<AuthorizationRoleResult> ExecuteAsync(
        Guid roleId,
        UpdateAuthorizationRoleRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActor(tenantContext, out var centerId, out var actorId) ||
            roleId == Guid.Empty)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var roleName = request.RoleName.Trim();
        var reason = request.Reason.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        if (string.IsNullOrWhiteSpace(roleName) || roleName.Length > 150 ||
            request.Description?.Length > 500 ||
            !request.Status.HasValue || !Enum.IsDefined(request.Status.Value) ||
            !ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion) ||
            expectedVersion == 0 ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
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
        if (role.RowVersion != expectedVersion)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var isArchiving = role.Status != AuthorizationRoleStatus.Archived &&
                          request.Status.Value == AuthorizationRoleStatus.Archived;
        if (isArchiving)
        {
            if (!await permissionEvaluator.HasPermissionAsync(
                    "authorization.roles.archive",
                    cancellationToken))
            {
                return AuthorizationRoleResult.Failure(ErrorCodes.AuthPermissionRequired);
            }
            if (role.IsSystemRole)
            {
                return AuthorizationRoleResult.Failure(ErrorCodes.InvalidStateTransition);
            }
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(
                isArchiving ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted,
                cancellationToken)
            : null;
        if (isArchiving && !await administratorGuard.HasAdministratorAfterAsync(
                changedRoleId: roleId,
                changedRoleActive: false,
                cancellationToken: cancellationToken))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return AuthorizationRoleResult.Failure(ErrorCodes.LastTenantAdmin);
        }

        var before = JsonSerializer.Serialize(new
        {
            role.RoleName,
            role.Description,
            Status = role.Status.ToString(),
            role.RowVersion
        });
        role.RoleName = roleName;
        role.Description = description;
        role.Status = request.Status.Value;
        role.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        role.UpdatedBy = actorId;
        role.RowVersion++;
        dbContext.Entry(role).Property(item => item.RowVersion).OriginalValue = expectedVersion;

        dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = actorId,
            ActionType = isArchiving ? "RoleArchived" : "RoleUpdated",
            TargetType = "Role",
            TargetId = role.RoleId.ToString("D"),
            BeforeData = before,
            AfterData = JsonSerializer.Serialize(new
            {
                role.RoleName,
                role.Description,
                Status = role.Status.ToString(),
                role.RowVersion
            }),
            Reason = reason,
            TraceId = NormalizeTraceId(traceId),
            CreatedAt = role.UpdatedAt,
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
            PermissionCodes = role.RolePermissions
                .Select(mapping => mapping.PermissionAccountType.Permission.PermissionCode)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            ActiveUserCount = role.UserAssignments.Count(assignment =>
                assignment.Status == UserRoleAssignmentStatus.Active),
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
