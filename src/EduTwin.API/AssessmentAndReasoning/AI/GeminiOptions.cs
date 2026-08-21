namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }

    public string? Model { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)
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
