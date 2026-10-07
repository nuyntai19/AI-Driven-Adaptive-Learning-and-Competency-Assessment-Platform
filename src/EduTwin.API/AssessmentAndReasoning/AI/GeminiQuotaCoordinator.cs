using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed record GeminiQuotaLease(string PoolId, Guid RequestId);

// SQL row locks coordinate capacity across API/worker instances, without holding a transaction during inference.
public sealed class GeminiQuotaCoordinator(IServiceScopeFactory scopes, TimeProvider clock)
{
    public async Task<GeminiQuotaLease> AcquireAsync(GeminiQuotaPoolOptions pool, string model, long estimatedTokens,
        TimeSpan timeout, CancellationToken token)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pool.ProjectId + "\n" + model)));
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        await EnsureRowAsync(db, id, token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var row = await LockedRowAsync(db, id, token);
        var state = JsonSerializer.Deserialize<GeminiQuotaLedger>(row.StateJson) ?? new();
        var now = clock.GetUtcNow().UtcDateTime;
        var requestId = Guid.NewGuid();
        var delay = state.TryReserve(pool, estimatedTokens, timeout, requestId, now);
        if (delay.HasValue) throw new AIAnalysisDeferredException(delay.Value);
        row.StateJson = JsonSerializer.Serialize(state);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(id, requestId);
    }

    public async Task CompleteAsync(GeminiQuotaLease lease, int? actualInputTokens, bool transientFailure, bool quotaFailure, CancellationToken token,
        TimeSpan? providerRetryAfter = null, bool dailyQuota = false)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var row = await LockedRowAsync(db, lease.PoolId, token);
        var state = JsonSerializer.Deserialize<GeminiQuotaLedger>(row.StateJson) ?? new();
        state.Complete(lease.RequestId, actualInputTokens, transientFailure, quotaFailure, clock.GetUtcNow().UtcDateTime, providerRetryAfter, dailyQuota);
        row.StateJson = JsonSerializer.Serialize(state);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
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
    public int ConsecutiveFailures { get; set; }
    public string Day { get; set; } = "";
    public int DayRequestCount { get; set; }

    public TimeSpan? TryReserve(GeminiQuotaPoolOptions pool, long inputTokens, TimeSpan timeout, Guid requestId, DateTime now)
    {
        Entries.RemoveAll(x => x.StartedAt <= (pool.RollingDailyWindow ? now.AddDays(-1) : now.AddMinutes(-1)) && (x.Completed || x.ExpiresAt <= now));
        var day = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles")).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (Day != day) { Day = day; DayRequestCount = 0; }
        if (CooldownUntil > now) return CooldownUntil.Value - now;
        if (Entries.Count(x => !x.Completed && x.ExpiresAt > now) >= pool.MaxConcurrentRequests) return TimeSpan.FromSeconds(2);
        var window = Entries.Where(x => x.StartedAt > now.AddMinutes(-1)).ToArray();
        if (pool.RequestsPerDay > 0 && (pool.RollingDailyWindow ? Entries.Count : DayRequestCount) >= pool.RequestsPerDay)
            return pool.RollingDailyWindow ? Entries.Min(x => x.StartedAt).AddDays(1) - now : UntilPacificReset(now);
        if (pool.TokensPerDay > 0 && Entries.Sum(x => x.InputTokens) + inputTokens > pool.TokensPerDay)
            return Entries.Count == 0 ? TimeSpan.FromDays(1) : Entries.Min(x => x.StartedAt).AddDays(1) - now;
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
            if (CooldownUntil is null || until > CooldownUntil) CooldownUntil = until;
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

public sealed class GeminiQuotaEntry
{
    public Guid RequestId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public long InputTokens { get; set; }
    public bool Completed { get; set; }
}
