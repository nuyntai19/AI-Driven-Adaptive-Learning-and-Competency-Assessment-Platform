namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public static class EvidenceConsistencyReasonCodes
{
    public const string ContradictionIncorrectAttemptNoError = "CONTRADICTION_INCORRECT_ATTEMPT_NO_ERROR";
    public const string ContradictionIncorrectAttemptHighQuality = "CONTRADICTION_INCORRECT_ATTEMPT_HIGH_QUALITY";
    public const string ContradictionCorrectAttemptFatalError = "CONTRADICTION_CORRECT_ATTEMPT_FATAL_ERROR";
    public const string ContradictionCorrectAttemptTopicFailure = "CONTRADICTION_CORRECT_ATTEMPT_TOPIC_FAILURE";
    public const string ContradictionNoErrorWithRootCauses = "CONTRADICTION_NO_ERROR_WITH_ROOT_CAUSES";
    public const string ContradictionSkippedWithReasoning = "CONTRADICTION_SKIPPED_WITH_REASONING";
    public const string ContradictionMetadataMismatch = "CONTRADICTION_METADATA_MISMATCH";
    public const string ContradictionQuestionAttemptMismatch = "CONTRADICTION_QUESTION_ATTEMPT_MISMATCH";

    public const string AnomalyZeroTimeHighReasoning = "ANOMALY_ZERO_TIME_HIGH_REASONING";
    public const string AnomalyUnrealisticCompletionTime = "ANOMALY_UNREALISTIC_COMPLETION_TIME";
    public const string AnomalyMissingReasoningTextHighQuality = "ANOMALY_MISSING_REASONING_TEXT_HIGH_QUALITY";

    public const string SemanticInvalidRootCauseMismatch = "SEMANTIC_INVALID_ROOT_CAUSE_MISMATCH";
    public const string SemanticInvalidRootCauseFormat = "SEMANTIC_INVALID_ROOT_CAUSE_FORMAT";
    public const string SemanticInvalidLanguageMismatch = "SEMANTIC_INVALID_LANGUAGE_MISMATCH";
}
