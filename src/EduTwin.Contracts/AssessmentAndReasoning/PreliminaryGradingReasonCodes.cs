namespace EduTwin.Contracts.AssessmentAndReasoning;

public static class PreliminaryGradingReasonCodes
{
    public const string NoAnswer = "NO_ANSWER";
    public const string ExactMatch = "EXACT_MATCH";
    public const string TextMismatch = "TEXT_MISMATCH";
    public const string TextMismatchInternalWhitespace = "TEXT_MISMATCH_INTERNAL_WHITESPACE";
    public const string NumericEquivalent = "NUMERIC_EQUIVALENT";
    public const string NumericMismatch = "NUMERIC_MISMATCH";
    public const string CoordinateEquivalent = "COORDINATE_EQUIVALENT";
    public const string CoordinateMismatch = "COORDINATE_MISMATCH";
    public const string ManualMode = "MANUAL_MODE";
    public const string InvalidReferenceAnswer = "INVALID_REFERENCE_ANSWER";
    public const string UnsupportedMathFormat = "UNSUPPORTED_MATH_FORMAT";
    public const string VoidedQuestion = "VOIDED_QUESTION";
    public const string AiProviderUnavailable = "AI_PROVIDER_UNAVAILABLE";
    public const string AiResponseInvalid = "AI_RESPONSE_INVALID";
    public const string TeacherOverride = "TEACHER_OVERRIDE";
}
