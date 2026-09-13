namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class AnalysisJobStatusDataDto
{
    public required string AnalysisJobId { get; set; }
    public required string AttemptId { get; set; }
    public required string Status { get; set; }
    public int RetryCount { get; set; }
    public bool Terminal { get; set; }
    public string? FeedbackUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? AttemptStatus { get; set; }
    public string? ErrorCode { get; set; }
}
