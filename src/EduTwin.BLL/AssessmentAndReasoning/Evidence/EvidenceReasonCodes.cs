namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public static class EvidenceReasonCodes
{
    public const string SourceRuleFallback = "SOURCE_RULE_FALLBACK";
    public const string SourceTypeUnsupported = "SOURCE_TYPE_UNSUPPORTED";
    public const string PreliminaryCorrectnessPending = "PRELIMINARY_CORRECTNESS_PENDING";
    public const string StructuralValidationFailed = "STRUCTURAL_VALIDATION_FAILED";
    public const string SemanticValidationFailed = "SEMANTIC_VALIDATION_FAILED";
    public const string ContradictionDetected = "CONTRADICTION_DETECTED";
    public const string AnomalyDetected = "ANOMALY_DETECTED";
    public const string RequiredEvidenceMissing = "REQUIRED_EVIDENCE_MISSING";
    public const string AIConfidenceMissing = "AI_CONFIDENCE_MISSING";
    public const string AIConfidenceBelow50 = "AI_CONFIDENCE_BELOW_50";
    public const string AIConfidence50To79 = "AI_CONFIDENCE_50_79";
    public const string AIConfidence80To100 = "AI_CONFIDENCE_80_100";
    public const string TeacherHumanConfirmed = "TEACHER_HUMAN_CONFIRMED";
    public const string TeacherOverrideInvalid = "TEACHER_OVERRIDE_INVALID";
    public const string HistoricalBackfillReviewOnly = "HISTORICAL_BACKFILL_REVIEW_ONLY";
}
