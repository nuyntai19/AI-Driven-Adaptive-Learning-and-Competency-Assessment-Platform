using System.Text.Json;
using EduTwin.BLL.Notifications;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.PlatformOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduTwin.BLL.Platform.Operations;

public sealed class OperationsMaintenance(EduTwinDbContext db, OperationalTelemetry telemetry,
    IOperationalHostProbe host, IOptions<PlatformOperationsOptions> options, TimeProvider clock)
{
    public async Task StoreCallsAsync(IReadOnlyList<PlatformAiCall> calls, CancellationToken ct)
    {
        var ids = calls.Select(x => x.CallId).ToArray();
        // At most 100 metadata calls per flush; use scalar UUID predicates supported by Oracle's MySQL provider.
        var existing = await NotificationRecipientScope.WhereIds(db.PlatformAiCalls.AsNoTracking(), x => x.CallId, ids).Select(x => x.CallId).ToArrayAsync(ct);
        foreach (var call in calls.Where(x => !existing.Contains(x.CallId)))
        { AiCostEstimator.Apply(call, options.Value.Prices); db.PlatformAiCalls.Add(call); }
        await db.SaveChangesAsync(ct);
    }

    public async Task CaptureStorageAsync(CancellationToken ct)
    {
        if (db.CurrentTenantId != AuthorizationBootstrapper.ReservedPlatformCenterId) throw new InvalidOperationException("Root execution scope required.");
        var now = clock.GetUtcNow().UtcDateTime;
        var eligible = db.Centers.AsNoTracking().Where(x => !x.IsDeleted && x.CenterId != AuthorizationBootstrapper.ReservedPlatformCenterId);
        var centers = await eligible
            .Select(x => new { x.CenterId, x.CenterName }).ToArrayAsync(ct);
        var ids = eligible.Select(x => x.CenterId);
        // Metadata aggregates only; Data.Length translates to SQL LENGTH, never fetch the blob.
        var attachments = await db.AttemptAttachments.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.CenterId))
            .GroupBy(x => x.CenterId).Select(g => new { CenterId = g.Key, Count = g.LongCount(), Bytes = g.Sum(x => x.FileSizeBytes) }).ToArrayAsync(ct);
        var images = await db.QuestionImages.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.CenterId))
            .GroupBy(x => x.CenterId).Select(g => new { CenterId = g.Key, Count = g.LongCount(), Bytes = g.Sum(x => (long)x.Data.Length) }).ToArrayAsync(ct);
        var usage = centers.Select(c => new StorageCenterUsage(c.CenterId, c.CenterName,
            attachments.SingleOrDefault(x => x.CenterId == c.CenterId)?.Count ?? 0,
            attachments.SingleOrDefault(x => x.CenterId == c.CenterId)?.Bytes ?? 0,
            images.SingleOrDefault(x => x.CenterId == c.CenterId)?.Count ?? 0,
            images.SingleOrDefault(x => x.CenterId == c.CenterId)?.Bytes ?? 0)).ToArray();
        var volume = host.Volume();
        var data = new PlatformStorageData(now, usage, volume.Total, volume.Free, host.Backup());
        var snapshot = await db.PlatformOpsSnapshots.SingleOrDefaultAsync(x => x.SnapshotKey == "storage", ct);
        if (snapshot is null) { snapshot = new() { SnapshotKey = "storage" }; db.PlatformOpsSnapshots.Add(snapshot); }
        snapshot.CapturedAt = now; snapshot.Payload = JsonSerializer.Serialize(data);
        await db.SaveChangesAsync(ct);
    }

    public async Task EvaluateAlertsAsync(CancellationToken ct)
    {
        if (db.CurrentTenantId != AuthorizationBootstrapper.ReservedPlatformCenterId) throw new InvalidOperationException("Root execution scope required.");
        var now = clock.GetUtcNow().UtcDateTime; var config = options.Value;
        var ids = db.Centers.AsNoTracking().Where(x => !x.IsDeleted && x.CenterId != db.CurrentTenantId).Select(x => x.CenterId);
        var jobs = db.AIAnalysisJobs.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.CenterId));
        var pending = await jobs.CountAsync(x => x.Status == AIJobStatus.Pending, ct);
        var failed = await jobs.CountAsync(x => x.Status == AIJobStatus.FailedTerminal && x.UpdatedAt >= now.AddHours(-1), ct);
        var beat = telemetry.Snapshot(); var volume = host.Volume(); var backup = host.Backup();
        bool Stale(DateTime? last) => now - (last ?? beat.ProcessStartedAt) > TimeSpan.FromSeconds(Math.Clamp(config.WorkerStaleSeconds, 60, 3600));
        var conditions = new[]
        {
            (Key: "queue_backlog", Active: pending >= Math.Max(1, config.PendingWarningCount), Title: "Hàng đợi AI đang tăng", Body: $"Có {pending} công việc đang chờ. Xem Vận hành hệ thống để kiểm tra quota và worker.", Permission: "platform.operations.read"),
            (Key: "terminal_failures", Active: failed >= Math.Max(1, config.FailedWarningCount), Title: "Công việc AI cần kiểm tra", Body: $"Có {failed} công việc lỗi kết thúc trong một giờ gần đây. Không tự chạy lại hoặc đổi điểm.", Permission: "platform.operations.read"),
            (Key: "worker_stale", Active: Stale(beat.AnalysisWorkerAt) || Stale(beat.PostProcessingWorkerAt), Title: "Worker AI thiếu tín hiệu hoạt động", Body: "Chưa nhận tín hiệu quét hàng đợi gần đây. Kiểm tra Vận hành hệ thống; đây không phải kết luận bài làm bị mất.", Permission: "platform.operations.read"),
            (Key: "volume_low", Active: volume.Total > 0 && volume.Free.HasValue && (double)volume.Free / volume.Total * 100 < Math.Clamp(config.VolumeFreeWarningPercent, 1, 50), Title: "Volume API sắp hết chỗ", Body: "Dung lượng trống volume API dưới ngưỡng cấu hình. Không tự xóa ảnh hoặc dữ liệu học tập.", Permission: "platform.storage.read"),
            (Key: "backup_stale", Active: backup.Status == "Failed" || backup.LastSuccessAt.HasValue && now - backup.LastSuccessAt.Value > TimeSpan.FromHours(Math.Max(1, config.BackupMaxAgeHours)), Title: "Cần kiểm tra sao lưu", Body: "Lượt sao lưu báo lỗi hoặc bản sao thành công đã quá ngưỡng tuổi cấu hình. Xem Lưu trữ & sao lưu.", Permission: "platform.storage.read")
        };
        foreach (var condition in conditions)
            await UpdateAlertAsync(condition.Key, condition.Active, condition.Title, condition.Body, condition.Permission, now, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAlertAsync(string key, bool active, string title, string body, string permission, DateTime now, CancellationToken ct)
    {
        var alert = await db.PlatformOpsAlerts.SingleOrDefaultAsync(x => x.AlertKey == key, ct);
        if (alert is null)
        {
            if (!active) return;
            alert = new() { AlertKey = key, EpisodeId = Guid.NewGuid(), IsActive = true, FirstSeenAt = now };
            db.PlatformOpsAlerts.Add(alert);
        }
        else if (active && !alert.IsActive)
        { alert.IsActive = true; alert.FirstSeenAt = now; alert.EpisodeId = Guid.NewGuid(); alert.NotifiedAt = null; }
        alert.LastSeenAt = now;
        var recovery = !active && alert.IsActive && alert.NotifiedAt.HasValue;
        if (!active) alert.IsActive = false;
        if (!recovery && (!active || alert.NotifiedAt.HasValue || now - alert.FirstSeenAt < TimeSpan.FromSeconds(Math.Clamp(options.Value.AlertSustainSeconds, 30, 3600)))) return;
        var recipients = await db.UserRoleAssignments.AsNoTracking().Where(x => x.Status == UserRoleAssignmentStatus.Active &&
            x.Role.Status == AuthorizationRoleStatus.Active && !x.Role.IsDeleted && x.User.Status == UserStatus.Active && !x.User.IsDeleted &&
            x.User.RoleName == UserRole.PlatformAdmin && x.Role.RolePermissions.Any(g => g.PermissionAccountType.Permission.Status == PermissionStatus.Active &&
                g.PermissionAccountType.Permission.PermissionCode == permission)).Select(x => x.UserId).Distinct().Take(500).ToArrayAsync(ct);
        if (recipients.Length == 0) return;
        var eventId = Guid.NewGuid();
        db.NotificationEvents.Add(new() { EventId = eventId, CenterId = db.CurrentTenantId,
            DeduplicationKey = $"platform-ops:{key}:{alert.EpisodeId:D}:{(recovery ? "recovered" : "warning")}",
            EventType = "SystemNotice", SenderName = "Vận hành EduTwin", Title = recovery ? "Đã phục hồi: " + title : title,
            Body = recovery ? "Điều kiện cảnh báo trước đó không còn vượt ngưỡng. Dữ liệu học tập được giữ nguyên." : body,
            RecipientUserIds = JsonSerializer.Serialize(recipients), CreatedAt = now });
        db.NotificationDispatches.Add(new() { CenterId = db.CurrentTenantId, EventId = eventId, AvailableAt = now });
        if (!recovery) alert.NotifiedAt = now;
    }
}
