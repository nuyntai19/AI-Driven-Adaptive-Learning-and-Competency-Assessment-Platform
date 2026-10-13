using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.PlatformOperations;

// Global operational metadata. No navigation to an Attempt, User or academic payload.
public sealed class PlatformAiCall
{
    public Guid CallId { get; set; }
    public DateTime StartedAt { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string PoolAlias { get; set; } = "";
    public string Stage { get; set; } = "other";
    public string Outcome { get; set; } = "failed";
    public long DurationMs { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? ThinkingTokens { get; set; }
    public long? CachedTokens { get; set; }
    public long? TotalTokens { get; set; }
    public string? PriceVersion { get; set; }
    public decimal? EstimatedUsd { get; set; }
}

public sealed class PlatformOpsSnapshot
{
    public string SnapshotKey { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public string Payload { get; set; } = "{}";
}

public sealed class PlatformOpsAlert : IHasRowVersion
{
    public string AlertKey { get; set; } = "";
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public bool IsActive { get; set; }
    public Guid EpisodeId { get; set; }
    public ulong RowVersion { get; set; }
}
