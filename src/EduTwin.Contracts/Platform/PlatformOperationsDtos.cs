namespace EduTwin.Contracts.Platform;

public sealed record OpsCenter(Guid CenterId, string CenterName);
public sealed record OpsCount(string Status, int Count);
public sealed record OpsJob(string JobId, Guid CenterId, string Status, byte RetryCount, DateTime CreatedAt,
    DateTime AvailableAt, DateTime? StartedAt, DateTime? LeaseUntil, string? ErrorCode);
public sealed record OpsHeartbeat(DateTime ProcessStartedAt, DateTime? AnalysisWorkerAt, DateTime? PostProcessingWorkerAt,
    DateTime? NotificationWorkerAt, long DroppedCalls, long BufferedCalls);
public sealed record OpsAlertDto(string Key, DateTime FirstSeenAt, DateTime? NotifiedAt);
public sealed record PlatformOperationsData(DateTime CapturedAt, bool DatabaseReachable, OpsHeartbeat Heartbeat,
    IReadOnlyList<OpsCenter> Centers, IReadOnlyList<OpsCount> Counts, double? OldestPendingSeconds,
    int ExpiredLeases, IReadOnlyList<OpsJob> Jobs, IReadOnlyList<OpsAlertDto> Alerts);
public sealed record AiUsageGroup(DateTime Day, string Provider, string Model, string Stage, int Calls,
    int FailedCalls, int TokenReportedCalls, long? InputTokens, long? OutputTokens, long? ThinkingTokens,
    long? CachedTokens, long? TotalTokens, int PricedCalls, decimal? EstimatedUsd, double AverageDurationMs);
public sealed record PlatformAiUsageData(DateTime FromUtc, DateTime ToUtc, DateTime? RecordingStartedAt,
    long DroppedCallsSinceProcessStart, DateTime ProcessStartedAt, IReadOnlyList<AiUsageGroup> Groups);
public sealed record StorageCenterUsage(Guid CenterId, string CenterName, long AttachmentCount, long AttachmentBytes,
    long QuestionImageCount, long QuestionImageBytes);
public sealed record BackupObservation(string Status = "Unknown", DateTime? LastAttemptAt = null,
    DateTime? LastSuccessAt = null, long? Bytes = null, string? Sha256 = null, DateTime? LastRestoreVerifiedAt = null,
    string? RestoreVerifiedSha256 = null, string? LastRestoreStatus = null);
public sealed record PlatformStorageData(DateTime? CapturedAt, IReadOnlyList<StorageCenterUsage> Centers,
    long? VolumeTotalBytes, long? VolumeFreeBytes, BackupObservation Backup);
