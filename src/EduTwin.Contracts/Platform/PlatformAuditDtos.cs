using System;
using System.Collections.Generic;
using System.Text.Json;

namespace EduTwin.Contracts.Platform;

public record PlatformAuditQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? ActionType { get; init; }
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public Guid? TargetCenterId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? TraceId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? Search { get; init; }
}

public record PlatformAuditItemDto
{
    public string AuditId { get; init; } = string.Empty;
    public Guid CenterId { get; init; }
    public Guid? TargetCenterId { get; init; }
    public string? TargetCenterCode { get; init; }
    public Guid? ActorUserId { get; init; }
    public string? ActorUsername { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string TargetType { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public JsonElement? BeforeData { get; init; }
    public JsonElement? AfterData { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public record PlatformAuditListData
{
    public IReadOnlyList<PlatformAuditItemDto> Items { get; init; } = Array.Empty<PlatformAuditItemDto>();
    public long TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
