using System.Collections.Concurrent;
using System.Threading.Channels;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.PlatformOperations;

namespace EduTwin.BLL.Platform.Operations;

public sealed class PlatformOperationsOptions
{
    public int SnapshotMinutes { get; set; } = 5;
    public int AlertSustainSeconds { get; set; } = 120;
    public int PendingWarningCount { get; set; } = 100;
    public int FailedWarningCount { get; set; } = 5;
    public int WorkerStaleSeconds { get; set; } = 300;
    public int BackupMaxAgeHours { get; set; } = 48;
    public int VolumeFreeWarningPercent { get; set; } = 10;
    public string? BackupManifestPath { get; set; }
    public List<AiPriceProfile> Prices { get; set; } = [];
}

public sealed class AiPriceProfile
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string PoolAlias { get; set; } = "";
    public string Version { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Billing { get; set; } = "Unknown";
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveUntilUtc { get; set; }
    public decimal InputUsdPerMillion { get; set; }
    public decimal OutputUsdPerMillion { get; set; }
    public decimal CachedUsdPerMillion { get; set; }
    public decimal ThinkingUsdPerMillion { get; set; }
    public bool OutputIncludesThinking { get; set; }
}

public static class AiUsageStage
{
    private static readonly AsyncLocal<string?> CurrentStage = new();
    public static string Current => CurrentStage.Value ?? "other";
    public static IDisposable Begin(string stage)
    {
        if (stage is not ("visual" or "grading" or "learning_path")) throw new ArgumentOutOfRangeException(nameof(stage));
        var previous = CurrentStage.Value; CurrentStage.Value = stage;
        return new Restore(() => CurrentStage.Value = previous);
    }
    private sealed class Restore(Action restore) : IDisposable { public void Dispose() => restore(); }
}

public interface IAiUsageRecorder { void Record(PlatformAiCall call); }

// Bounded, non-blocking producer: telemetry must never hold up a provider result.
public sealed class OperationalTelemetry(TimeProvider clock) : IAiUsageRecorder
{
    public Channel<PlatformAiCall> Calls { get; } = Channel.CreateBounded<PlatformAiCall>(new BoundedChannelOptions(1000)
    { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<string, DateTime> _beats = new();
    private long _dropped;
    public DateTime ProcessStartedAt { get; } = clock.GetUtcNow().UtcDateTime;
    public void Beat(string worker) => _beats[worker] = clock.GetUtcNow().UtcDateTime;
    public void Drop(long count) => Interlocked.Add(ref _dropped, count);
    public void Record(PlatformAiCall call)
    {
        if (call.Provider is not ("Gemini" or "Groq") || call.Model.Length is < 1 or > 120 ||
            call.Stage is not ("other" or "visual" or "grading" or "learning_path") ||
            call.Outcome is not ("success" or "failed" or "cancelled" or "rate_limited") ||
            !System.Text.RegularExpressions.Regex.IsMatch(call.PoolAlias, "^pool-[0-9]{1,3}$") ||
            call.CallId == Guid.Empty || call.DurationMs < 0 ||
            new[] { call.InputTokens, call.OutputTokens, call.ThinkingTokens, call.CachedTokens, call.TotalTokens }.Any(n => n < 0))
        { Drop(1); return; }
        if (!Calls.Writer.TryWrite(call)) Drop(1);
    }
    public OpsHeartbeat Snapshot() => new(ProcessStartedAt, BeatAt("analysis"), BeatAt("post_processing"),
        BeatAt("notifications"), Interlocked.Read(ref _dropped), Calls.Reader.Count);
    private DateTime? BeatAt(string name) => _beats.TryGetValue(name, out var value) ? value : null;
}

public static class AiCostEstimator
{
    public static void Apply(PlatformAiCall call, IEnumerable<AiPriceProfile> profiles)
    {
        // Refuse overlapping profiles instead of guessing a price or silently changing historical cost.
        call.EstimatedUsd = null; call.PriceVersion = null;
        var matches = profiles.Where(p => p.Provider == call.Provider && p.Model == call.Model && p.PoolAlias == call.PoolAlias &&
            p.EffectiveFromUtc <= call.StartedAt && (p.EffectiveUntilUtc is null || call.StartedAt < p.EffectiveUntilUtc)).ToArray();
        if (matches.Length != 1) return;
        var p = matches[0];
        if (p.Version.Length is < 1 or > 80 || !Uri.TryCreate(p.SourceUrl, UriKind.Absolute, out var source) || source.Scheme != "https" ||
            p.Billing is not ("Free" or "Paid") || new[] { p.InputUsdPerMillion, p.OutputUsdPerMillion, p.CachedUsdPerMillion, p.ThinkingUsdPerMillion }.Any(n => n < 0)) return;
        if (call.InputTokens is not { } input || call.OutputTokens is not { } output) return;
        var cached = call.CachedTokens ?? 0;
        var thinking = call.ThinkingTokens ?? 0;
        if (input < cached || (p.OutputIncludesThinking && output < thinking)) return;
        call.PriceVersion = p.Version;
        call.EstimatedUsd = p.Billing == "Free" ? 0 : decimal.Round(((input - cached) * p.InputUsdPerMillion + cached * p.CachedUsdPerMillion +
            (output - (p.OutputIncludesThinking ? thinking : 0)) * p.OutputUsdPerMillion + thinking * p.ThinkingUsdPerMillion) / 1_000_000m, 8);
    }
}

public sealed class OperationsQueryCache(TimeProvider clock)
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, (DateTime Until, object Value)> _values = [];
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> factory, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var now = clock.GetUtcNow().UtcDateTime;
            if (_values.TryGetValue(key, out var entry) && entry.Until > now) return (T)entry.Value;
            var value = await factory();
            foreach (var expired in _values.Where(x => x.Value.Until <= now).Select(x => x.Key).ToArray()) _values.Remove(expired);
            if (_values.Count >= 100) _values.Remove(_values.Keys.First());
            _values[key] = (now.AddSeconds(30), value!); return value;
        }
        finally { _gate.Release(); }
    }
}

public interface IOperationalHostProbe
{
    (long? Total, long? Free) Volume();
    BackupObservation Backup();
}
