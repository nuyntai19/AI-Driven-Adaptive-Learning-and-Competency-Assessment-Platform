namespace EduTwin.BLL.AssessmentAndReasoning.Feedback;

public static class AIProcessingPresentation
{
    public static string? Reason(string? code) => code switch
    {
        "AI_PROVIDER_DAILY_QUOTA_WAIT" => "QuotaWait",
        "AI_PROVIDER_CAPACITY_WAIT" => "CapacityWait",
        "AI_PROVIDER_TIMEOUT" => "Timeout",
        "AI_PROVIDER_REQUEST_FAILED" or "AI_PROVIDER_CONFIGURATION_INVALID" => "ProviderUnavailable",
        "AI_RESPONSE_JSON_INVALID" or "AI_RESPONSE_SHAPE_INVALID" or "AI_RESPONSE_SEMANTIC_INVALID"
            or "AI_PROVIDER_RESPONSE_EMPTY" or "AI_PROVIDER_RESPONSE_INVALID" => "ResponseInvalid",
        "AttachmentStorageUnavailable" => "AttachmentUnavailable",
        "AI_ANALYSIS_ATTEMPT_FAILED" or "AI_REQUEST_CONTEXT_INVALID" => "UnknownFailure",
        _ => null
    };
}
