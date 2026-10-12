namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed class AIAnalysisValidationException : Exception, IAIAnalysisFailure
{
    private AIAnalysisValidationException(string errorCode, string message, AIResponseValidationRule rule, string? diagnosticDetail = null)
        : base(message)
    {
        ErrorCode = errorCode;
        ValidationRule = rule;
        DiagnosticDetail = diagnosticDetail;
    }

    public string ErrorCode { get; }
    public AIResponseValidationRule ValidationRule { get; }
    // Server-defined diagnostic token only; never include provider text or student content.
    public string? DiagnosticDetail { get; }

    internal static AIAnalysisValidationException JsonInvalid() =>
        new("AI_RESPONSE_JSON_INVALID", "AI response JSON is invalid.", AIResponseValidationRule.Json);

    internal static AIAnalysisValidationException ShapeInvalid() =>
        new("AI_RESPONSE_SHAPE_INVALID", "AI response shape is invalid.", AIResponseValidationRule.Shape);

    internal static AIAnalysisValidationException SemanticInvalid(AIResponseValidationRule rule = AIResponseValidationRule.General, string? diagnosticDetail = null) =>
        new("AI_RESPONSE_SEMANTIC_INVALID", "AI response semantics are invalid.", rule, diagnosticDetail);
}
