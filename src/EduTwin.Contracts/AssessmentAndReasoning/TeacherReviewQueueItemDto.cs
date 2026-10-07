namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class TeacherReviewQueueItemDto
{
    public string AttemptId { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string AssignmentTitle { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public uint? QuestionOrderIndex { get; set; }
    public int AssignmentQuestionCount { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string AttemptStatus { get; set; } = string.Empty;
    public string AnswerEvaluationMode { get; set; } = string.Empty;
    public string? TeacherSolution { get; set; }
    public string? ExpectedReasoning { get; set; }
    public string? MethodDetected { get; set; }
    public bool HasAttachment { get; set; }
    public string AnalysisId { get; set; } = string.Empty;
    public string FinalAnswer { get; set; } = string.Empty;
    public string? AnswerDisplayLatex { get; set; }
    public string? ReasoningText { get; set; }
    public bool IsFallback { get; set; }
    public decimal? ReasoningQuality { get; set; }
    public decimal? OriginalReasoningQuality { get; set; }
    public string? AnalysisFeedback { get; set; }
    public string? FeedbackOrigin { get; set; }
    public string? AiSolution { get; set; }
    public decimal? SuggestedScore { get; set; }
    public RubricGrade? SuggestedRubricGrade { get; set; }
    public bool UsesAlternativeMethod { get; set; }
    public string? AnswerAssessment { get; set; }
    public decimal? AnalysisConfidence { get; set; }
    public string? ErrorType { get; set; }
    public EvidenceDecisionDto Evidence { get; set; } = new();
    public DateTime SubmittedAt { get; set; }
    public bool HasStudentReviewRequest { get; set; }
    public string? StudentReviewReason { get; set; }
    public string TeacherFinalReviewStatus { get; set; } = "Pending";
    public uint FinalReviewVersion { get; set; }
    public IReadOnlyList<TeacherReviewQuestionOptionDto> Options { get; set; } = Array.Empty<TeacherReviewQuestionOptionDto>();
    public string? CorrectAnswer { get; set; }
    public decimal? MaxScore { get; set; }
    public decimal? AwardedScore { get; set; }
    public bool? IsCorrect { get; set; }
    public bool HasTeacherOverride { get; set; }
    public decimal? OverrideAwardedScore { get; set; }
    public string? OverrideReason { get; set; }
    public string? TeacherFeedback { get; set; }
    public string? ReviewDecision { get; set; }
    public uint OverrideVersion { get; set; }
    public EduTwin.Contracts.CurriculumAndQuestions.GradingCriteria? GradingCriteria { get; set; }
    public RubricGrade? RubricGrade { get; set; }
}

public sealed class TeacherReviewQuestionOptionDto
{
    public string OptionId { get; set; } = string.Empty;
    public string OptionLabel { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
