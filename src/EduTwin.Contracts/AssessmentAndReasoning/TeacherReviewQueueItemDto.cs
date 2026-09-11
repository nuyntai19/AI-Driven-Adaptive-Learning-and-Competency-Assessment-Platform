namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class TeacherReviewQueueItemDto
{
    public string AttemptId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string QuestionId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string AnalysisId { get; set; } = string.Empty;
    public string FinalAnswer { get; set; } = string.Empty;
    public string? ReasoningText { get; set; }
    public bool IsFallback { get; set; }
    public decimal? ReasoningQuality { get; set; }
    public string? AnalysisFeedback { get; set; }
    public decimal? AnalysisConfidence { get; set; }
    public EvidenceDecisionDto Evidence { get; set; } = new();
    public DateTime SubmittedAt { get; set; }
}
