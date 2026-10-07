using System.Text.Json;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";
    public const string ListKeyConfigurationName = "GEMINI_LIST_KEY";

    /// <summary>Preferred JSON array; when configured, replaces all legacy key settings.</summary>
    public string? ListKey { get; set; }

    public string? ApiKey { get; set; }

    public string? BackupKeys { get; set; }

    public string? BackupKeys_2 { get; set; }

    public string? Model { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConcurrentRequests { get; set; } = 2;
    public List<GeminiQuotaPoolOptions> QuotaPools { get; set; } = [];

    public void LoadQuotaPoolsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            QuotaPools = JsonSerializer.Deserialize<List<GeminiQuotaPoolOptions>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw GeminiAdapterException.ConfigurationInvalid();
        }
        catch (JsonException) { throw GeminiAdapterException.ConfigurationInvalid(); }
    }

    public IReadOnlyList<GeminiQuotaPoolOptions> GetQuotaPools(int keyCount) => QuotaPools.Count == 0
        ? [new GeminiQuotaPoolOptions { ProjectId = "unverified-shared-project", KeyIndexes = Enumerable.Range(0, keyCount).ToArray(), MaxConcurrentRequests = MaxConcurrentRequests }]
        : QuotaPools;

    public IReadOnlyList<string> GetAllApiKeys()
    {
        if (!string.IsNullOrWhiteSpace(ListKey))
        {
            string?[]? configuredKeys;
            try
            {
                configuredKeys = JsonSerializer.Deserialize<string?[]>(ListKey);
            }
            catch (JsonException)
            {
                // JSON diagnostics can contain credentials. Never retain or log them.
                throw GeminiAdapterException.ConfigurationInvalid();
            }

            if (configuredKeys is null || configuredKeys.Any(string.IsNullOrWhiteSpace))
                throw GeminiAdapterException.ConfigurationInvalid();

            return configuredKeys.Select(key => key!.Trim())
                .Distinct(StringComparer.Ordinal).ToArray();
        }

        var keys = new List<string>();

        void AddKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var parts = raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part) && !keys.Contains(part))
                {
                    keys.Add(part);
                }
            }
        }

        AddKey(ApiKey);
        AddKey(BackupKeys);
        AddKey(BackupKeys_2);

        return keys;
    }

    public void Validate()
    {
        var allKeys = GetAllApiKeys();
        if (QuotaPools is null || QuotaPools.Any(p => p is null || p.KeyIndexes is null))
            throw GeminiAdapterException.ConfigurationInvalid();
        var pools = GetQuotaPools(allKeys.Count);
        var indexes = pools.SelectMany(p => p.KeyIndexes).ToArray();
        if (allKeys.Count == 0
            || MaxConcurrentRequests is < 1 or > 32
            || string.IsNullOrWhiteSpace(Model)
            || Timeout <= TimeSpan.Zero
            || Timeout > TimeSpan.FromSeconds(120)
            || pools.Any(p => string.IsNullOrWhiteSpace(p.ProjectId) || p.ProjectId.Length > 100 || p.MaxConcurrentRequests is < 1 or > 32 ||
                p.RequestsPerMinute < 0 || p.InputTokensPerMinute < 0 || p.RequestsPerDay < 0 || p.KeyIndexes.Length == 0)
            || pools.Select(p => p.ProjectId).Distinct(StringComparer.Ordinal).Count() != pools.Count
            || indexes.Length != allKeys.Count || indexes.Distinct().Count() != allKeys.Count || indexes.Any(i => i < 0 || i >= allKeys.Count))
        {
            throw GeminiAdapterException.ConfigurationInvalid();
        }
    }
}

public sealed class GeminiQuotaPoolOptions
{
    public string ProjectId { get; set; } = "";
    // Zero-based indexes in the deduplicated GEMINI_LIST_KEY list; never put keys in logs/metadata.
    public int[] KeyIndexes { get; set; } = [];
    public int MaxConcurrentRequests { get; set; } = 2;
    // 0 = unknown, not unlimited provider quota; shared cooldown still handles 429.
    public int RequestsPerMinute { get; set; }
    public long InputTokensPerMinute { get; set; }
    public int RequestsPerDay { get; set; }
    public long TokensPerDay { get; set; }
    public bool RollingDailyWindow { get; set; }
}

public sealed class GeminiAdapterException : Exception
{
    private GeminiAdapterException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }

    internal static GeminiAdapterException ConfigurationInvalid() =>
        new("AI_PROVIDER_CONFIGURATION_INVALID", "AI provider configuration is invalid.");

    internal static GeminiAdapterException Timeout() =>
        new("AI_PROVIDER_TIMEOUT", "AI provider request timed out.");

    internal static GeminiAdapterException RequestFailed() =>
        new("AI_PROVIDER_REQUEST_FAILED", "AI provider request failed.");

    internal static GeminiAdapterException ResponseEmpty() =>
        new("AI_PROVIDER_RESPONSE_EMPTY", "AI provider returned no usable response.");

    internal static GeminiAdapterException ResponseInvalid() =>
        new("AI_PROVIDER_RESPONSE_INVALID", "AI provider response is invalid.");
}
