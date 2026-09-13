using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Platform;

public sealed class PlatformAuditService(
    EduTwinDbContext dbContext,
    ITenantContext tenantContext) : IPlatformAuditService
{
    private static readonly string[] SensitiveKeyFragments =
    [
        "password", "hash", "token", "secret", "cookie", "authorization", "authversion",
        "credential", "privatekey", "score", "points", "awardedscore", "reasoning",
        "answer", "submission", "analysisresult", "digitaltwin", "feedback"
    ];

    public async Task<PlatformResult<PlatformAuditListData>> ListAuditLogsAsync(
        PlatformAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved ||
            tenantContext.CenterId != AuthorizationBootstrapper.ReservedPlatformCenterId ||
            tenantContext.Role != nameof(UserRole.PlatformAdmin))
        {
            return PlatformResult<PlatformAuditListData>.Failure(
                ErrorCodes.ForbiddenResource,
                "Chỉ Quản trị viên Nền tảng trong Root Tenant PLATFORM mới có quyền truy cập nhật ký kiểm toán nền tảng.");
        }

        if (query.Page < 1)
        {
            return PlatformResult<PlatformAuditListData>.Failure(
                ErrorCodes.ValidationFailed,
                "Trang phải lớn hơn hoặc bằng 1.");
        }

        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc)
        {
            return PlatformResult<PlatformAuditListData>.Failure(
                ErrorCodes.ValidationFailed,
                "Thời gian bắt đầu (fromUtc) không được sau thời gian kết thúc (toUtc).");
        }

        // Invariant: Scoped ONLY to PLATFORM tenant operations. Tenant-internal logs are strictly excluded.
        var baseQuery = dbContext.AuthorizationAuditLogs
            .AsNoTracking()
            .Where(a => a.CenterId == AuthorizationBootstrapper.ReservedPlatformCenterId);

        if (!string.IsNullOrWhiteSpace(query.ActionType))
        {
            var actionType = query.ActionType.Trim();
            baseQuery = baseQuery.Where(a => a.ActionType == actionType);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            var targetType = query.TargetType.Trim();
            baseQuery = baseQuery.Where(a => a.TargetType == targetType);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetId))
        {
            var targetId = query.TargetId.Trim();
            baseQuery = baseQuery.Where(a => a.TargetId == targetId);
        }

        if (query.TargetCenterId.HasValue && query.TargetCenterId.Value != Guid.Empty)
        {
            baseQuery = baseQuery.Where(a => a.TargetCenterId == query.TargetCenterId.Value);
        }

        if (query.ActorUserId.HasValue && query.ActorUserId.Value != Guid.Empty)
        {
            baseQuery = baseQuery.Where(a => a.ActorUserId == query.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TraceId))
        {
            var traceId = query.TraceId.Trim();
            baseQuery = baseQuery.Where(a => a.TraceId == traceId);
        }

        if (query.FromUtc.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.CreatedAt >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.CreatedAt <= query.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(a =>
                a.Reason.Contains(search) ||
                a.TargetId.Contains(search) ||
                (a.TargetCenter != null && a.TargetCenter.CenterCode.Contains(search)));
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AuthorizationAuditId)
            .Skip((query.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.AuthorizationAuditId,
                a.CenterId,
                a.TargetCenterId,
                TargetCenterCode = a.TargetCenter != null ? a.TargetCenter.CenterCode : null,
                a.ActorUserId,
                ActorUsername = a.ActorUser != null ? a.ActorUser.Username : null,
                a.ActionType,
                a.TargetType,
                a.TargetId,
                a.BeforeData,
                a.AfterData,
                a.Reason,
                a.TraceId,
                a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var dtoList = items.Select(a => new PlatformAuditItemDto
        {
            AuditId = a.AuthorizationAuditId.ToString(),
            CenterId = a.CenterId,
            TargetCenterId = a.TargetCenterId,
            TargetCenterCode = a.TargetCenterCode,
            ActorUserId = a.ActorUserId,
            ActorUsername = a.ActorUsername,
            ActionType = a.ActionType,
            TargetType = a.TargetType,
            TargetId = a.TargetId,
            BeforeData = SanitizeAuditJson(a.BeforeData),
            AfterData = SanitizeAuditJson(a.AfterData),
            Reason = a.Reason,
            TraceId = a.TraceId,
            CreatedAt = a.CreatedAt
        }).ToList();

        return PlatformResult<PlatformAuditListData>.Success(new PlatformAuditListData
        {
            Items = dtoList,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = pageSize
        });
    }

    public async Task<PlatformResult<PlatformAuditItemDto>> GetAuditLogByIdAsync(
        ulong auditId,
        CancellationToken cancellationToken = default)
    {
        if (!tenantContext.IsResolved ||
            tenantContext.CenterId != AuthorizationBootstrapper.ReservedPlatformCenterId ||
            tenantContext.Role != nameof(UserRole.PlatformAdmin))
        {
            return PlatformResult<PlatformAuditItemDto>.Failure(
                ErrorCodes.ForbiddenResource,
                "Chỉ Quản trị viên Nền tảng trong Root Tenant PLATFORM mới có quyền xem nhật ký kiểm toán nền tảng.");
        }

        var audit = await dbContext.AuthorizationAuditLogs
            .AsNoTracking()
            .Where(a => a.AuthorizationAuditId == auditId &&
                        a.CenterId == AuthorizationBootstrapper.ReservedPlatformCenterId)
            .Select(a => new
            {
                a.AuthorizationAuditId,
                a.CenterId,
                a.TargetCenterId,
                TargetCenterCode = a.TargetCenter != null ? a.TargetCenter.CenterCode : null,
                a.ActorUserId,
                ActorUsername = a.ActorUser != null ? a.ActorUser.Username : null,
                a.ActionType,
                a.TargetType,
                a.TargetId,
                a.BeforeData,
                a.AfterData,
                a.Reason,
                a.TraceId,
                a.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (audit is null)
        {
            return PlatformResult<PlatformAuditItemDto>.Failure(
                ErrorCodes.ResourceNotFound,
                $"Không tìm thấy bản ghi kiểm toán nền tảng với ID '{auditId}'.");
        }

        return PlatformResult<PlatformAuditItemDto>.Success(new PlatformAuditItemDto
        {
            AuditId = audit.AuthorizationAuditId.ToString(),
            CenterId = audit.CenterId,
            TargetCenterId = audit.TargetCenterId,
            TargetCenterCode = audit.TargetCenterCode,
            ActorUserId = audit.ActorUserId,
            ActorUsername = audit.ActorUsername,
            ActionType = audit.ActionType,
            TargetType = audit.TargetType,
            TargetId = audit.TargetId,
            BeforeData = SanitizeAuditJson(audit.BeforeData),
            AfterData = SanitizeAuditJson(audit.AfterData),
            Reason = audit.Reason,
            TraceId = audit.TraceId,
            CreatedAt = audit.CreatedAt
        });
    }

    public static JsonElement? SanitizeAuditJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var sanitizedNode = SanitizeNode(doc.RootElement);
            if (sanitizedNode is null)
            {
                return null;
            }

            using var sanitizedDoc = JsonDocument.Parse(sanitizedNode.ToJsonString());
            return sanitizedDoc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static JsonNode? SanitizeNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var prop in element.EnumerateObject())
                {
                    if (IsSensitiveKey(prop.Name))
                    {
                        continue;
                    }

                    var child = SanitizeNode(prop.Value);
                    if (child is not null)
                    {
                        obj.Add(prop.Name, child);
                    }
                }
                return obj;

            case JsonValueKind.Array:
                var arr = new JsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    var child = SanitizeNode(item);
                    if (child is not null)
                    {
                        arr.Add(child);
                    }
                }
                return arr;

            case JsonValueKind.String:
                var str = element.GetString() ?? string.Empty;
                if (str.StartsWith("eyJh", StringComparison.OrdinalIgnoreCase) ||
                    str.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonValue.Create("[REDACTED_TOKEN]");
                }
                return JsonValue.Create(str);

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var lVal))
                {
                    return JsonValue.Create(lVal);
                }
                if (element.TryGetDouble(out var dVal))
                {
                    return JsonValue.Create(dVal);
                }
                return JsonValue.Create(element.GetRawText());

            case JsonValueKind.True:
                return JsonValue.Create(true);

            case JsonValueKind.False:
                return JsonValue.Create(false);

            case JsonValueKind.Null:
            default:
                return null;
        }
    }

    private static bool IsSensitiveKey(string keyName)
    {
        var lower = keyName.ToLowerInvariant();
        return SensitiveKeyFragments.Any(frag => lower.Contains(frag));
    }
}
