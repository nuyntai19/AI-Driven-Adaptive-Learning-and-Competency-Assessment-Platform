using System.Reflection;
using System.Text.Json;
using EduTwin.API.Controllers;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform.Operations;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.PlatformOperations;
using EduTwin.DAL.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.Platform;

public sealed class PlatformOperationsTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public DateTime Value = Now; public override DateTimeOffset GetUtcNow() => new(Value); }
    private sealed class Probe : IOperationalHostProbe
    {
        public (long? Total, long? Free) Volume() => (1000, 500);
        public BackupObservation Backup() => new();
    }
    private static (EduTwinDbContext Db, PlatformOperationsService Service, Clock Time, OperationalTelemetry Telemetry, TenantContext Tenant) Create(string role = "PlatformAdmin", Guid? scope = null, Guid? dbScope = null)
    {
        var center = scope ?? AuthorizationBootstrapper.ReservedPlatformCenterId;
        var tenant = new TenantContext(); tenant.Initialize(center, Guid.NewGuid(), role, 1);
        var accessor = new TenantContext(); accessor.Initialize(dbScope ?? center, Guid.NewGuid(), role, 1);
        var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor);
        var time = new Clock(); var telemetry = new OperationalTelemetry(time);
        return (db, new(db, tenant, telemetry, new(time), time), time, telemetry, tenant);
    }
    private static PlatformAiCall Call() => new() { CallId = Guid.NewGuid(), StartedAt = Now, Provider = "Gemini", Model = "test-model", PoolAlias = "pool-1", Stage = "visual", Outcome = "success", DurationMs = 1200 };
    private static AiPriceProfile Price() => new() { Provider = "Gemini", Model = "test-model", PoolAlias = "pool-1", Version = "verified-1", SourceUrl = "https://ai.google.dev/gemini-api/docs/pricing", Billing = "Paid", EffectiveFromUtc = Now.AddDays(-1), InputUsdPerMillion = 2, CachedUsdPerMillion = 0.5m, OutputUsdPerMillion = 10, ThinkingUsdPerMillion = 10 };

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("CenterManager")]
    public async Task NonPlatformAccounts_DeniedAtServiceEvenInsideRoot(string role)
    {
        var (db, service, _, _, _) = Create(role); using (db)
        { Assert.Equal(ErrorCodes.ForbiddenResource, (await service.OperationsAsync(null, default)).ErrorCode); Assert.Equal(ErrorCodes.ForbiddenResource, (await service.AiUsageAsync(7, default)).ErrorCode); Assert.Equal(ErrorCodes.ForbiddenResource, (await service.StorageAsync(default)).ErrorCode); }
    }
    [Fact] public async Task PlatformInTenantOrMismatchedDbScope_Denied()
    {
        foreach (var mismatch in new[] {false, true})
        { var f = mismatch ? Create(dbScope: Guid.NewGuid()) : Create(scope: Guid.NewGuid()); using (f.Db) Assert.Equal(ErrorCodes.ForbiddenResource, (await f.Service.OperationsAsync(null, default)).ErrorCode); }
    }
    [Theory] [InlineData(0)] [InlineData(2)] [InlineData(31)] [InlineData(1000)]
    public async Task UnsupportedUsageWindow_Denied(int days)
    { var f = Create(); using (f.Db) Assert.Equal(ErrorCodes.ValidationFailed, (await f.Service.AiUsageAsync(days, default)).ErrorCode); }

    [Fact] public async Task QueueAggregate_ExplicitCentersBoundedProjectionAndNoAcademicFields()
    {
        var f = Create(); using var db = f.Db; var a = Guid.NewGuid(); var b = Guid.NewGuid(); var deleted = Guid.NewGuid();
        foreach (var id in new[] {a, b, deleted}) db.Centers.Add(new Center {CenterId = id, CenterCode = id.ToString(), CenterName = "Test center", Timezone = "UTC", IsDeleted = id == deleted, CreatedAt = Now, UpdatedAt = Now});
        for (var n = 1; n <= 25; n++) db.AIAnalysisJobs.Add(new AIAnalysisJob {AnalysisJobId = (ulong)n, CenterId = a, AttemptId = (ulong)n, Status = AIJobStatus.Pending, CreatedAt = Now.AddMinutes(-n), UpdatedAt = Now, AvailableAt = Now, CorrelationId = "private-answer", LastErrorMessage = "private-score-and-answer", LastErrorCode = "private-student-name"});
        db.AIAnalysisJobs.Add(new AIAnalysisJob {AnalysisJobId = 26, CenterId = b, Status = AIJobStatus.FailedTerminal, CorrelationId = "private", CreatedAt = Now, UpdatedAt = Now, AvailableAt = Now});
        db.AIAnalysisJobs.Add(new AIAnalysisJob {AnalysisJobId = 27, CenterId = deleted, Status = AIJobStatus.Pending, CorrelationId = "private", CreatedAt = Now, UpdatedAt = Now, AvailableAt = Now});
        await db.SaveChangesAsync();
        var result = (await f.Service.OperationsAsync(a, default)).Data!;
        Assert.Equal(25, result.Counts.Single().Count); Assert.Equal(20, result.Jobs.Count); Assert.Equal(1500, result.OldestPendingSeconds);
        var json = JsonSerializer.Serialize(result); Assert.DoesNotContain("private", json); Assert.DoesNotContain("AttemptId", json); Assert.DoesNotContain("StudentId", json); Assert.All(result.Jobs, job => Assert.Equal("OPERATION_FAILED", job.ErrorCode));
    }
    [Fact] public async Task EmptyStorageAndAi_ExplicitUnknownNotFabricated()
    {
        var f = Create(); using var db = f.Db;
        var storage = (await f.Service.StorageAsync(default)).Data!; Assert.Null(storage.CapturedAt); Assert.Equal("Unknown", storage.Backup.Status); Assert.Empty(storage.Centers);
        var usage = (await f.Service.AiUsageAsync(7, default)).Data!; Assert.Null(usage.RecordingStartedAt); Assert.Empty(usage.Groups);
    }
    [Fact] public async Task UsagePerInvocation_DedupAndSnapshotPrice()
    {
        var f = Create(); using var db = f.Db; var call = Call(); call.InputTokens = 100; call.OutputTokens = 20; call.TotalTokens = 130;
        var settings = new PlatformOperationsOptions {Prices = [Price()]}; var maintenance = new OperationsMaintenance(db, f.Telemetry, new Probe(), Options.Create(settings), f.Time);
        await maintenance.StoreCallsAsync([call], default); await maintenance.StoreCallsAsync([call], default);
        Assert.Single(db.PlatformAiCalls); settings.Prices[0].InputUsdPerMillion = 900;
        var result = (await f.Service.AiUsageAsync(1, default)).Data!.Groups.Single(); Assert.Equal(130, result.TotalTokens); Assert.Equal(0.0004m, result.EstimatedUsd); Assert.Equal(1, result.Calls);
    }
    [Fact] public void Pricing_NoDoubleCountCachedOrProviderTotal()
    { var call = Call(); call.InputTokens = 100; call.CachedTokens = 40; call.OutputTokens = 20; call.ThinkingTokens = 10; call.TotalTokens = 130; AiCostEstimator.Apply(call, [Price()]); Assert.Equal(0.00044m, call.EstimatedUsd); }
    [Fact] public void Pricing_InclusiveThinkingIsSubtractedFromOutput()
    { var call = Call(); call.InputTokens = 100; call.OutputTokens = 30; call.ThinkingTokens = 10; var p = Price(); p.OutputIncludesThinking = true; AiCostEstimator.Apply(call, [p]); Assert.Equal(0.0005m, call.EstimatedUsd); }
    [Fact] public void Pricing_UnknownOverlapExpiredOrMissingTokenNeverBecomesZero()
    {
        var call = Call(); AiCostEstimator.Apply(call, [Price()]); Assert.Null(call.EstimatedUsd);
        call.InputTokens = 100; call.OutputTokens = 20; var p = Price(); p.EffectiveUntilUtc = Now; AiCostEstimator.Apply(call, [p]); Assert.Null(call.EstimatedUsd);
        AiCostEstimator.Apply(call, [Price(), Price()]); Assert.Null(call.EstimatedUsd); AiCostEstimator.Apply(call, []); Assert.Null(call.EstimatedUsd);
    }
    [Fact] public void Pricing_VerifiedFreeIsGenuineZero()
    { var call = Call(); call.InputTokens = 100; call.OutputTokens = 20; var p = Price(); p.Billing = "Free"; AiCostEstimator.Apply(call, [p]); Assert.Equal(0m, call.EstimatedUsd); }
    [Fact] public void Recorder_IsBoundedAndRejectsUnsafeLabels()
    { var telemetry = new OperationalTelemetry(new Clock()); for (var n = 0; n < 1001; n++) telemetry.Record(Call()); Assert.Equal(1, telemetry.Snapshot().DroppedCalls); Assert.Equal(1000, telemetry.Snapshot().BufferedCalls); var bad = Call(); bad.Stage = "student-sensitive-data"; telemetry.Record(bad); Assert.Equal(2, telemetry.Snapshot().DroppedCalls); }
    [Fact] public async Task StageScope_IsolatedAcrossAsyncWorkAndRestores()
    { Assert.Equal("other", AiUsageStage.Current); using (AiUsageStage.Begin("grading")) { await Task.Yield(); Assert.Equal("grading", AiUsageStage.Current); using (AiUsageStage.Begin("visual")) Assert.Equal("visual", AiUsageStage.Current); Assert.Equal("grading", AiUsageStage.Current); } Assert.Equal("other", AiUsageStage.Current); }
    [Fact] public void Permissions_AreRootOnlySensitiveNonDelegableAndEndpointSpecific()
    {
        foreach (var (method, code) in new[] {("Operations", "platform.operations.read"), ("AiUsage", "platform.ai_usage.read"), ("Storage", "platform.storage.read")})
        { var p = AuthorizationPermissionCatalog.CreatePermissions().Single(x => x.PermissionCode == code); Assert.True(p.IsSensitive); Assert.False(p.IsDelegable); Assert.Equal(UserRole.PlatformAdmin, AuthorizationPermissionCatalog.CreateAccountTypeMappings().Single(x => x.PermissionId == p.PermissionId).AccountType);
            Assert.Contains(typeof(PlatformOperationsController).GetMethod(method)!.GetCustomAttributes<AuthorizeAttribute>(), x => x.Policy == code); }
    }
    [Fact] public async Task CacheExpiresWithoutUnboundedKeys()
    { var time = new Clock(); var cache = new OperationsQueryCache(time); var reads = 0; Task<int> Read() => Task.FromResult(++reads); Assert.Equal(1, await cache.GetAsync("key", Read, default)); Assert.Equal(1, await cache.GetAsync("key", Read, default)); time.Value = time.Value.AddSeconds(31); Assert.Equal(2, await cache.GetAsync("key", Read, default)); }

    [Fact] public async Task Alerts_SustainedDeduplicatedAndRecoveredViaOutboxOnly()
    {
        var f = Create(); using var db = f.Db; await db.Database.EnsureCreatedAsync();
        var permission = await db.PermissionAccountTypes.Include(x => x.Permission).SingleAsync(x => x.AccountType == UserRole.PlatformAdmin && x.Permission.PermissionCode == "platform.operations.read");
        var user = new EduTwin.DAL.IdentityAndTenancy.User {UserId = f.Tenant.UserId!.Value, CenterId = db.CurrentTenantId, Username = "ops_test", DisplayName = "Ops", PasswordHash = "test", RoleName = UserRole.PlatformAdmin, Status = UserStatus.Active, CreatedAt = Now, UpdatedAt = Now};
        var role = new EduTwin.DAL.IdentityAndTenancy.AuthorizationRole {RoleId = Guid.NewGuid(), CenterId = db.CurrentTenantId, RoleCode = "TEST_OPS", RoleName = "Ops", AccountType = UserRole.PlatformAdmin, Status = AuthorizationRoleStatus.Active, CreatedAt = Now, UpdatedAt = Now};
        role.RolePermissions.Add(new() {CenterId = db.CurrentTenantId, RoleId = role.RoleId, PermissionId = permission.PermissionId, AccountType = UserRole.PlatformAdmin, PermissionAccountType = permission, GrantedByUserId = user.UserId, GrantedAt = Now});
        db.UserRoleAssignments.Add(new() {CenterId = db.CurrentTenantId, UserId = user.UserId, RoleId = role.RoleId, AccountType = UserRole.PlatformAdmin, Status = UserRoleAssignmentStatus.Active, User = user, Role = role, AssignedAt = Now}); await db.SaveChangesAsync();
        var maintenance = new OperationsMaintenance(db, f.Telemetry, new Probe(), Options.Create(new PlatformOperationsOptions {AlertSustainSeconds = 30}), f.Time);
        async Task Observe(bool active, int seconds)
        { await maintenance.UpdateAlertAsync("queue_backlog", active, "Cảnh báo", "Metadata only", "platform.operations.read", Now.AddSeconds(seconds), default); await db.SaveChangesAsync(); }
        await Observe(true, 0); await Observe(true, 29); Assert.Empty(db.NotificationEvents);
        await Observe(true, 31); await Observe(true, 61); Assert.Single(db.NotificationEvents);
        await Observe(false, 62); await Observe(false, 90); Assert.Equal(2, db.NotificationEvents.Count());
        Assert.Equal(2, db.NotificationDispatches.Count()); Assert.Empty(db.NotificationDeliveries);
        await Observe(true, 100); await Observe(true, 131); Assert.Equal(3, db.NotificationEvents.Count());
        Assert.All(db.NotificationEvents, e => Assert.Equal(db.CurrentTenantId, e.CenterId));
    }
}
