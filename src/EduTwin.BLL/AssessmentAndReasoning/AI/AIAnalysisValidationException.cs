namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed class AIAnalysisValidationException : Exception
{
    private AIAnalysisValidationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }

    internal static AIAnalysisValidationException JsonInvalid() =>
        new("AI_RESPONSE_JSON_INVALID", "AI response JSON is invalid.");

    internal static AIAnalysisValidationException ShapeInvalid() =>
        new("AI_RESPONSE_SHAPE_INVALID", "AI response shape is invalid.");

    internal static AIAnalysisValidationException SemanticInvalid() =>
        new("AI_RESPONSE_SEMANTIC_INVALID", "AI response semantics are invalid.");
}
