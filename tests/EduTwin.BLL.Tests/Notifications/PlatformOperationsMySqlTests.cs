using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform.Operations;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.Organization;
using EduTwin.DAL.PlatformOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

// Reuses the established isolated UUID schema fixture, never the user's database.
public sealed partial class NotificationMySqlTests
{
    private sealed class OpsProbe : IOperationalHostProbe
    {
        public (long? Total, long? Free) Volume() => (10000, 4000);
        public BackupObservation Backup() => new();
    }
    [NotificationMySqlFact]
    public async Task PlatformOperations_MigrationStorageLengthAndUsageGroup_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student);
        await Academic(f, 1);
        await using var db = f.Context();
        var now = DateTime.UtcNow;
        var secondCenter = Guid.NewGuid();
        db.Centers.Add(new Center {CenterId = secondCenter, CenterCode = "OPS_SECOND", CenterName = "Second isolated center", Status = CenterStatus.Active, Timezone = "Asia/Bangkok", CreatedAt = now, UpdatedAt = now});
        // One known byte-array in the synthetic tenant; no live images are read.
        db.QuestionImages.Add(new() {CenterId = f.CenterId, QuestionId = 1, Data = new byte[137], Sha256 = new string('a', 64), CreatedAt = now}); await db.SaveChangesAsync();
        var telemetry = new OperationalTelemetry(TimeProvider.System);
        var root = new TenantContext(); root.Initialize(AuthorizationBootstrapper.ReservedPlatformCenterId, Guid.NewGuid(), "PlatformAdmin", 1);
        await using var adminDb = f.Context(root);
        var maintenance = new OperationsMaintenance(adminDb, telemetry, new OpsProbe(), Options.Create(new PlatformOperationsOptions()), TimeProvider.System);
        await maintenance.CaptureStorageAsync(default);
        var calls = Enumerable.Range(0, 2).Select(_ => new PlatformAiCall {CallId = Guid.NewGuid(), StartedAt = now, Provider = "Gemini", Model = "synthetic", PoolAlias = "pool-1", Stage = "visual", Outcome = "success", DurationMs = 1200, InputTokens = 100, OutputTokens = 20, TotalTokens = 130}).ToArray();
        await maintenance.StoreCallsAsync(calls, default);
        await maintenance.StoreCallsAsync(calls, default);
        await maintenance.EvaluateAlertsAsync(default);
        var service = new PlatformOperationsService(adminDb, root, telemetry, new(TimeProvider.System), TimeProvider.System);
        var storage = (await service.StorageAsync(default)).Data!; Assert.Equal(2, storage.Centers.Count); Assert.Equal(137, storage.Centers.Single(c => c.CenterId == f.CenterId).QuestionImageBytes); Assert.Equal(1, storage.Centers.Single(c => c.CenterId == f.CenterId).QuestionImageCount);
        var usage = (await service.AiUsageAsync(7, default)).Data!.Groups.Single(); Assert.Equal(260, usage.TotalTokens); Assert.Equal(2, usage.Calls); Assert.Null(usage.EstimatedUsd); Assert.Equal(1200, usage.AverageDurationMs);
        var queue = await service.OperationsAsync(null, default); Assert.True(queue.IsSuccess); Assert.Equal(2, queue.Data!.Centers.Count); Assert.Empty(queue.Data.Jobs);
        Assert.True((await service.OperationsAsync(secondCenter, default)).IsSuccess);
        Assert.False(db.Database.HasPendingModelChanges());
        var monitoringIds = await db.Permissions.Where(x => x.PermissionCode == "platform.operations.read" || x.PermissionCode == "platform.ai_usage.read" || x.PermissionCode == "platform.storage.read").Select(x => x.PermissionId).ToArrayAsync();
        Assert.Equal(3, monitoringIds.Length);
        var monitoringMappings = await db.PermissionAccountTypes.Where(x => x.Permission.PermissionCode == "platform.operations.read" || x.Permission.PermissionCode == "platform.ai_usage.read" || x.Permission.PermissionCode == "platform.storage.read").ToArrayAsync();
        Assert.Equal(3, monitoringMappings.Length);
        Assert.All(monitoringMappings, mapping => Assert.Equal(UserRole.PlatformAdmin, mapping.AccountType));
    }
}
