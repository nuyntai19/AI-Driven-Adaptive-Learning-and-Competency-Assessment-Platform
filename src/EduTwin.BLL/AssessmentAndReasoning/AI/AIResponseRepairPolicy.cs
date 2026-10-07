namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public static class AIResponseRepairPolicy
{
    public static AIResponseValidationRule? Resolve(byte retries, string? code, string? detail)
    {
        if (retries == 0) return null;
        var isResponseFailure = code?.StartsWith("AI_RESPONSE_", StringComparison.Ordinal) == true
            || code is "AI_PROVIDER_RESPONSE_EMPTY" or "AI_PROVIDER_RESPONSE_INVALID";
        var marker = detail?.StartsWith("Repair:", StringComparison.Ordinal) == true;
        if (!isResponseFailure && !marker) return null;
        var value = marker ? detail![7..] : detail;
        if (value?.Length <= 50 && Enum.TryParse<AIResponseValidationRule>(value, out var rule) && Enum.IsDefined(rule)) return rule;
        return isResponseFailure ? AIResponseValidationRule.General : null;
    }
}
