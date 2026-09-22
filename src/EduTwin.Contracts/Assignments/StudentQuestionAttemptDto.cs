using System;

namespace EduTwin.Contracts.Assignments;

public class StudentQuestionAttemptDto
{
    public string AttemptId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FinalAnswer { get; set; } = string.Empty;
    public string? ReasoningText { get; set; }
    public decimal Confidence { get; set; }
    public uint TimeSpentSeconds { get; set; }
    public uint AnswerChanges { get; set; }
    public bool Skipped { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? AwardedScore { get; set; }
    public decimal? MaxScore { get; set; }
}
