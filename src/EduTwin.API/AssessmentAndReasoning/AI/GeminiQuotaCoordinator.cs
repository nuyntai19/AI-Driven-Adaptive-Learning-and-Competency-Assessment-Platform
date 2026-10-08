using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed record GeminiQuotaLease(string PoolId, Guid RequestId, string? GlobalPoolId = null);

// SQL row locks coordinate capacity across API/worker instances, without holding a transaction during inference.
public sealed class GeminiQuotaCoordinator(IServiceScopeFactory scopes, TimeProvider clock)
{
    public static string GlobalCapacityPoolId { get; } =
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Gemini:global-concurrency:v1")));

    public async Task<GeminiQuotaLease> AcquireAsync(GeminiQuotaPoolOptions pool, string model, long estimatedTokens,
        TimeSpan timeout, CancellationToken token, int? globalMaxConcurrentRequests = null)
    {
        if (globalMaxConcurrentRequests is < 1 or > 32) throw GeminiAdapterException.ConfigurationInvalid();
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pool.ProjectId + "\n" + model)));
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        if (globalMaxConcurrentRequests.HasValue) await EnsureRowAsync(db, GlobalCapacityPoolId, token);
        await EnsureRowAsync(db, id, token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        // Always lock global before project/model, including release, to avoid deadlocks.
        AIProviderQuotaState? globalRow = null;
        GeminiConcurrencyLedger? globalState = null;
        if (globalMaxConcurrentRequests.HasValue)
        {
            globalRow = await LockedRowAsync(db, GlobalCapacityPoolId, token);
            globalState = JsonSerializer.Deserialize<GeminiConcurrencyLedger>(globalRow.StateJson) ?? new();
        }
        var row = await LockedRowAsync(db, id, token);
        var state = JsonSerializer.Deserialize<GeminiQuotaLedger>(row.StateJson) ?? new();
        var now = clock.GetUtcNow().UtcDateTime;
        var requestId = Guid.NewGuid();
        if (globalState?.TryReserve(globalMaxConcurrentRequests!.Value, timeout, requestId, now) is { } globalDelay)
            throw new AIAnalysisDeferredException(globalDelay, blocksAllPools: true);
        var delay = state.TryReserve(pool, estimatedTokens, timeout, requestId, now);
        if (delay.HasValue) throw new AIAnalysisDeferredException(delay.Value, state.WaitingReason);
        // Neither reservation is saved unless both checks succeed. A blocked pool
        // consumes neither a global slot nor a request/token quota reservation.
        if (globalRow is not null) globalRow.StateJson = JsonSerializer.Serialize(globalState);
        row.StateJson = JsonSerializer.Serialize(state);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(id, requestId, globalRow?.PoolId);
    }

    public async Task<TimeSpan?> CompleteAsync(GeminiQuotaLease lease, int? actualInputTokens, bool transientFailure, bool quotaFailure, CancellationToken token,
        TimeSpan? providerRetryAfter = null, bool dailyQuota = false)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (lease.GlobalPoolId is not null)
        {
            var globalRow = await LockedRowAsync(db, lease.GlobalPoolId, token);
            var globalState = JsonSerializer.Deserialize<GeminiConcurrencyLedger>(globalRow.StateJson) ?? new();
            globalState.Complete(lease.RequestId);
            globalRow.StateJson = JsonSerializer.Serialize(globalState);
        }
        var row = await LockedRowAsync(db, lease.PoolId, token);
        var state = JsonSerializer.Deserialize<GeminiQuotaLedger>(row.StateJson) ?? new();
        var now = clock.GetUtcNow().UtcDateTime;
        state.Complete(lease.RequestId, actualInputTokens, transientFailure, quotaFailure, now, providerRetryAfter, dailyQuota);
        row.StateJson = JsonSerializer.Serialize(state);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return transientFailure && state.CooldownUntil > now ? state.CooldownUntil.Value - now : null;
    }

    private static async Task EnsureRowAsync(EduTwinDbContext db, string id, CancellationToken token)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ai_provider_quota_states (pool_id,state_json,row_version) VALUES ({id},'{{}}',1) ON DUPLICATE KEY UPDATE pool_id = pool_id", token);
        else if (!await db.AIProviderQuotaStates.AnyAsync(x => x.PoolId == id, token))
        {
            db.AIProviderQuotaStates.Add(new AIProviderQuotaState { PoolId = id });
            await db.SaveChangesAsync(token);
            db.ChangeTracker.Clear();
        }
    }

    private static async Task<AIProviderQuotaState> LockedRowAsync(EduTwinDbContext db, string id, CancellationToken token)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pool_id FROM ai_provider_quota_states WHERE pool_id = {id} FOR UPDATE", token);
        return await db.AIProviderQuotaStates.SingleAsync(x => x.PoolId == id, token);
    }
}

public sealed class GeminiQuotaLedger
{
    public List<GeminiQuotaEntry> Entries { get; set; } = [];
    public DateTime? CooldownUntil { get; set; }
    public string CooldownReason { get; set; } = "AI_PROVIDER_CAPACITY_WAIT";
    [JsonIgnore] public string WaitingReason { get; private set; } = "AI_PROVIDER_CAPACITY_WAIT";
    public int ConsecutiveFailures { get; set; }
    public string Day { get; set; } = "";
    public int DayRequestCount { get; set; }

    public TimeSpan? TryReserve(GeminiQuotaPoolOptions pool, long inputTokens, TimeSpan timeout, Guid requestId, DateTime now)
    {
        WaitingReason = "AI_PROVIDER_CAPACITY_WAIT";
        Entries.RemoveAll(x => x.StartedAt <= (pool.RollingDailyWindow ? now.AddDays(-1) : now.AddMinutes(-1)) && (x.Completed || x.ExpiresAt <= now));
        var day = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles")).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (Day != day) { Day = day; DayRequestCount = 0; }
        if (CooldownUntil > now) { WaitingReason = CooldownReason; return CooldownUntil.Value - now; }
        if (Entries.Count(x => !x.Completed && x.ExpiresAt > now) >= pool.MaxConcurrentRequests) return TimeSpan.FromSeconds(2);
        var window = Entries.Where(x => x.StartedAt > now.AddMinutes(-1)).ToArray();
        if (pool.RequestsPerDay > 0 && (pool.RollingDailyWindow ? Entries.Count : DayRequestCount) >= pool.RequestsPerDay)
        {
            WaitingReason = "AI_PROVIDER_DAILY_QUOTA_WAIT";
            return pool.RollingDailyWindow ? Entries.Min(x => x.StartedAt).AddDays(1) - now : UntilPacificReset(now);
        }
        if (pool.TokensPerDay > 0 && Entries.Sum(x => x.InputTokens) + inputTokens > pool.TokensPerDay)
        {
            WaitingReason = "AI_PROVIDER_DAILY_QUOTA_WAIT";
            return Entries.Count == 0 ? TimeSpan.FromDays(1) : Entries.Min(x => x.StartedAt).AddDays(1) - now;
        }
        if (pool.InputTokensPerMinute > 0 && inputTokens > pool.InputTokensPerMinute) throw GeminiAdapterException.ConfigurationInvalid();
        if ((pool.RequestsPerMinute > 0 && window.Length >= pool.RequestsPerMinute) ||
            (pool.InputTokensPerMinute > 0 && window.Sum(x => x.InputTokens) + inputTokens > pool.InputTokensPerMinute))
            return window.Length == 0 ? TimeSpan.FromSeconds(60) : window.Min(x => x.StartedAt).AddMinutes(1) - now;
        Entries.Add(new GeminiQuotaEntry { RequestId = requestId, StartedAt = now, ExpiresAt = now + timeout + TimeSpan.FromSeconds(15), InputTokens = inputTokens });
        DayRequestCount++;
        return null;
    }

    public void Complete(Guid id, int? actualTokens, bool transient, bool quota, DateTime now,
        TimeSpan? providerRetryAfter = null, bool dailyQuota = false)
    {
        var entry = Entries.FirstOrDefault(x => x.RequestId == id);
        if (entry is not null) { entry.Completed = true; if (actualTokens.HasValue) entry.InputTokens = actualTokens.Value; }
        if (transient)
        {
            ConsecutiveFailures = Math.Min(ConsecutiveFailures + 1, 8);
            var seconds = Math.Min(300, (quota ? 60 : 2) * Math.Pow(2, ConsecutiveFailures - 1)) + Random.Shared.NextDouble();
            var delay = dailyQuota ? UntilPacificReset(now) : TimeSpan.FromSeconds(seconds);
            if (providerRetryAfter > delay) delay = providerRetryAfter.Value;
            var until = now + delay;
            if (CooldownUntil is null || until > CooldownUntil)
            {
                CooldownUntil = until;
                CooldownReason = dailyQuota ? "AI_PROVIDER_DAILY_QUOTA_WAIT" : "AI_PROVIDER_CAPACITY_WAIT";
            }
        }
        else ConsecutiveFailures = 0; // A success must not clear another in-flight call's cooldown.
    }

    private static TimeSpan UntilPacificReset(DateTime now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var tomorrow = TimeZoneInfo.ConvertTimeFromUtc(now, zone).Date.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(tomorrow, zone) - now;
    }
}

// Capacity only: completed/expired calls consume no slots and no daily quota.
// Stored separately from project/model ledgers, shared by every Gemini caller.
public sealed class GeminiConcurrencyLedger
{
    public List<GeminiQuotaEntry> Entries { get; set; } = [];

    public TimeSpan? TryReserve(int limit, TimeSpan timeout, Guid requestId, DateTime now)
    {
        Entries.RemoveAll(x => x.Completed || x.ExpiresAt <= now);
        if (Entries.Count >= limit) return TimeSpan.FromSeconds(2);
        Entries.Add(new() { RequestId = requestId, StartedAt = now, ExpiresAt = now + timeout + TimeSpan.FromSeconds(15) });
        return null;
    }

    public void Complete(Guid requestId) => Entries.RemoveAll(x => x.RequestId == requestId);
}

public sealed class GeminiQuotaEntry
{
    public Guid RequestId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public long InputTokens { get; set; }
    public bool Completed { get; set; }
}
