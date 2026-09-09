using System.Text.Json;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class ListAuthorizationAuditUseCase(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : IListAuthorizationAuditUseCase
{
    public async Task<ListAuthorizationAuditResult> ExecuteAsync(
        AuthorizationAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved ||
            !tenantContext.CenterId.HasValue || tenantContext.CenterId.Value == Guid.Empty ||
            !tenantContext.UserId.HasValue || tenantContext.UserId.Value == Guid.Empty)
        {
            return ListAuthorizationAuditResult.Failure(ErrorCodes.ResourceNotFound);
        }
        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            query.ActionType?.Length > 64 ||
            query.From.HasValue && query.To.HasValue && query.From > query.To ||
            query.ActorUserId == Guid.Empty || query.TargetUserId == Guid.Empty)
        {
            return ListAuthorizationAuditResult.Failure(ErrorCodes.ValidationFailed);
        }

        var actionType = string.IsNullOrWhiteSpace(query.ActionType)
            ? null
            : query.ActionType.Trim();
        var audits = dbContext.AuthorizationAuditLogs.AsNoTracking().AsQueryable();
        if (query.From.HasValue)
        {
            audits = audits.Where(audit => audit.CreatedAt >= query.From.Value);
        }
        if (query.To.HasValue)
        {
            audits = audits.Where(audit => audit.CreatedAt <= query.To.Value);
        }
        if (query.ActorUserId.HasValue)
        {
            audits = audits.Where(audit => audit.ActorUserId == query.ActorUserId.Value);
        }
        if (query.TargetUserId.HasValue)
        {
            audits = audits.Where(audit => audit.TargetUserId == query.TargetUserId.Value);
        }
        if (actionType is not null)
        {
            audits = audits.Where(audit => audit.ActionType == actionType);
        }

        var totalItems = await audits.LongCountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)query.PageSize);
        if ((query.Page - 1L) * query.PageSize >= totalItems)
        {
            return ListAuthorizationAuditResult.Success([], totalItems, totalPages);
        }

        var rows = await audits
            .OrderByDescending(audit => audit.CreatedAt)
            .ThenByDescending(audit => audit.AuthorizationAuditId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var data = rows.Select(audit => new AuthorizationAuditDto
        {
            AuthorizationAuditId = audit.AuthorizationAuditId,
            ActorUserId = audit.ActorUserId,
            ActionType = audit.ActionType,
            TargetType = audit.TargetType,
            TargetId = audit.TargetId,
            TargetUserId = audit.TargetUserId,
            PermissionCode = audit.PermissionCode,
            Before = ParseJson(audit.BeforeData),
            After = ParseJson(audit.AfterData),
            Reason = audit.Reason,
            TraceId = audit.TraceId,
            CreatedAt = audit.CreatedAt
        }).ToArray();
        return ListAuthorizationAuditResult.Success(data, totalItems, totalPages);
    }

    private static JsonElement? ParseJson(string? value)
    {
        if (value is null)
        {
            return null;
        }
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
