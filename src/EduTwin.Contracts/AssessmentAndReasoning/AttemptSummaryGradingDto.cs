namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class AttemptSummaryGradingDto
{
    public bool? IsCorrect { get; set; }
    public decimal? AwardedScore { get; set; }
    public decimal MaxScore { get; set; }
    public bool Skipped { get; set; }
}
