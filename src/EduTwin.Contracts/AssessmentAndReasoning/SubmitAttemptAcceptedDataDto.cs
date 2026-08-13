namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class SubmitAttemptAcceptedDataDto
{
    public required string AttemptId { get; init; }
    public required string AnalysisJobId { get; init; }
    public required string AttemptStatus { get; init; }
    public required string JobStatus { get; init; }
    public required string PollUrl { get; init; }
    public int PollAfterMilliseconds { get; init; } = 3000;
}
