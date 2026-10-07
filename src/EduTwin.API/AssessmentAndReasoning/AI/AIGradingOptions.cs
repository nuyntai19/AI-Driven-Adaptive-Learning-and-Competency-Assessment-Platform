namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class AIGradingOptions
{
    public string Provider { get; set; } = "Gemini";
    public string? GeminiModel { get; set; }
    // Explicit deployment gate, not a confidence score returned by an AI.
    public bool AlternativeProfileApproved { get; set; }
    public bool MicroBatchEnabled { get; set; }
    public int BatchSize { get; set; } = 3;
    public int MaxImages { get; set; } = 3;
    public int MaxInputCharacters { get; set; } = 24000;
    public int MaxPendingItems { get; set; } = 128;
    public TimeSpan BatchWindow { get; set; } = TimeSpan.FromMilliseconds(150);
    public string? GroqApiKey { get; set; }
    public string GroqModel { get; set; } = "qwen/qwen3.8-27b";
    public string GroqOrganizationId { get; set; } = "unverified-groq-organization";
    public int GroqRequestsPerMinute { get; set; } = 30;
    public int GroqRequestsPerDay { get; set; } = 1000;
    public long GroqTokensPerMinute { get; set; } = 8000;
    public long GroqTokensPerDay { get; set; } = 200000;

    public string Model(GeminiOptions gemini) => Provider == "Groq" ? GroqModel
        : string.IsNullOrWhiteSpace(GeminiModel) ? gemini.Model ?? "" : GeminiModel;
    public string Profile(GeminiOptions gemini) =>
        $"{Provider}:{Model(gemini)}:method-agnostic-v2:temperature-{(Provider == "Gemini" && Model(gemini).StartsWith("gemini-3", StringComparison.Ordinal) ? 1 : 0)}:microbatch-v1:{(MicroBatchEnabled ? BatchSize : 1)}:{EduTwin.BLL.AssessmentAndReasoning.AI.AIAnalysisContract.SchemaVersion}";

    public void Validate(GeminiOptions gemini)
    {
        if (Provider is not ("Gemini" or "Groq") || BatchSize is < 1 or > 5 || MaxImages is < 1 or > 3
            || MaxInputCharacters is < 1000 or > 100000 || MaxPendingItems is < 5 or > 1024
            || BatchWindow <= TimeSpan.Zero || BatchWindow > TimeSpan.FromSeconds(1)
            || string.IsNullOrWhiteSpace(Model(gemini)) || Model(gemini).Length > 100
            || ((Provider != "Gemini" || !string.IsNullOrWhiteSpace(GeminiModel) && GeminiModel != gemini.Model) && !AlternativeProfileApproved)
            || Provider == "Groq" && (string.IsNullOrWhiteSpace(GroqApiKey) || string.IsNullOrWhiteSpace(GroqOrganizationId)
                || GroqRequestsPerMinute < 1 || GroqRequestsPerDay < 1 || GroqTokensPerMinute < 1 || GroqTokensPerDay < 1))
            throw GeminiAdapterException.ConfigurationInvalid();
        if (Provider == "Gemini") gemini.Validate();
    }
}
