namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }

    public string? BackupKeys { get; set; }

    public string? BackupKeys_2 { get; set; }

    public string? Model { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public IReadOnlyList<string> GetAllApiKeys()
    {
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
        if (allKeys.Count == 0
            || string.IsNullOrWhiteSpace(Model)
            || Timeout <= TimeSpan.Zero
            || Timeout > TimeSpan.FromSeconds(120))
        {
            throw GeminiAdapterException.ConfigurationInvalid();
        }
    }
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
