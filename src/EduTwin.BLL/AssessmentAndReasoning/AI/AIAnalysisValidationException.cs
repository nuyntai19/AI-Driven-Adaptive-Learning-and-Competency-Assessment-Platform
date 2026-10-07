namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed class AIAnalysisValidationException : Exception, IAIAnalysisFailure
{
    private AIAnalysisValidationException(string errorCode, string message, AIResponseValidationRule rule)
        : base(message)
    {
        ErrorCode = errorCode;
        ValidationRule = rule;
    }

    public string ErrorCode { get; }
    public AIResponseValidationRule ValidationRule { get; }

    internal static AIAnalysisValidationException JsonInvalid() =>
        new("AI_RESPONSE_JSON_INVALID", "AI response JSON is invalid.", AIResponseValidationRule.Json);

    internal static AIAnalysisValidationException ShapeInvalid() =>
        new("AI_RESPONSE_SHAPE_INVALID", "AI response shape is invalid.", AIResponseValidationRule.Shape);

    internal static AIAnalysisValidationException SemanticInvalid(AIResponseValidationRule rule = AIResponseValidationRule.General) =>
        new("AI_RESPONSE_SEMANTIC_INVALID", "AI response semantics are invalid.", rule);
}
