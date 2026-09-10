namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class TeacherOverrideRequest
{
    public decimal ReasoningQuality { get; set; }
    public ErrorType ErrorType { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public decimal? AwardedScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public uint OverrideVersion { get; set; }
}
