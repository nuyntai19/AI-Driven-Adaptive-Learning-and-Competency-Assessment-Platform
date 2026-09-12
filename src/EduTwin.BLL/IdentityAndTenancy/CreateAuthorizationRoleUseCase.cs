using System.Text.Json;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class CreateAuthorizationRoleUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : ICreateAuthorizationRoleUseCase
{
    public async Task<AuthorizationRoleResult> ExecuteAsync(
        CreateAuthorizationRoleRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveActor(tenantContext, out var centerId, out var actorId))
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var roleCode = request.RoleCode.Trim();
        var roleName = request.RoleName.Trim();
        var description = NormalizeOptional(request.Description);
        if (string.IsNullOrWhiteSpace(roleCode) || roleCode.Length > 64 ||
            !System.Text.RegularExpressions.Regex.IsMatch(roleCode, "^[A-Z][A-Z0-9_]*$") ||
            string.IsNullOrWhiteSpace(roleName) || roleName.Length > 150 ||
            request.Description?.Length > 500 ||
            !request.AccountType.HasValue ||
            !Enum.IsDefined(request.AccountType.Value))
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.AccountType.Value == UserRole.PlatformAdmin)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.AuthPrivilegeEscalation);
        }

        var actorExists = await dbContext.Users.AnyAsync(user =>
            user.UserId == actorId && user.Status == UserStatus.Active,
            cancellationToken);
        if (!actorExists)
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (await dbContext.AuthorizationRoles.AnyAsync(
                role => role.RoleCode == roleCode,
                cancellationToken))
        {
            return AuthorizationRoleResult.Failure(ErrorCodes.DuplicateResource);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var role = new AuthorizationRole
        {
            RoleId = Guid.NewGuid(),
            CenterId = centerId,
            RoleCode = roleCode,
            RoleName = roleName,
            AccountType = request.AccountType.Value,
            Description = description,
            IsSystemRole = false,
            Status = AuthorizationRoleStatus.Active,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
            IsDeleted = false,
            RowVersion = 1
        };
        var audit = new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = actorId,
            ActionType = "RoleCreated",
            TargetType = "Role",
            TargetId = role.RoleId.ToString("D"),
            AfterData = JsonSerializer.Serialize(new
            {
                role.RoleCode,
                role.RoleName,
                AccountType = role.AccountType.ToString(),
                Status = role.Status.ToString()
            }),
            Reason = "Tạo role trong trung tâm.",
            TraceId = NormalizeTraceId(traceId),
            CreatedAt = now,
            CreatedBy = actorId
        };

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        dbContext.AuthorizationRoles.Add(role);
        dbContext.AuthorizationAuditLogs.Add(audit);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (IsRoleCodeConflict(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return AuthorizationRoleResult.Failure(ErrorCodes.DuplicateResource);
        }

        return AuthorizationRoleResult.Success(new AuthorizationRoleDto
        {
            RoleId = role.RoleId,
            RoleCode = role.RoleCode,
            RoleName = role.RoleName,
            AccountType = role.AccountType.ToString(),
            Description = role.Description,
            IsSystemRole = false,
            Status = role.Status.ToString(),
            PermissionCodes = [],
            ActiveUserCount = 0,
            RowVersion = role.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeTraceId(string traceId) =>
        string.IsNullOrWhiteSpace(traceId)
            ? "unknown"
            : traceId[..Math.Min(traceId.Length, 64)];

    private static bool IsRoleCodeConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    "ux_roles_center_id_role_code",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
