using System;

namespace EduTwin.Contracts.Assignments;

public sealed class AssignmentResultSummaryDto
{
    public int TotalQuestionCount { get; set; }
    public int AnsweredQuestionCount { get; set; }
    public int EvaluatedQuestionCount { get; set; }
    public int CorrectQuestionCount { get; set; }
    public int IncorrectQuestionCount { get; set; }
    public int PendingQuestionCount { get; set; }
    public string ResultStatus { get; set; } = "Processing";
    public string TeacherFinalReviewStatus { get; set; } = "Pending";
    public decimal? InternalAwardedScore { get; set; }
    public decimal InternalMaxScore { get; set; }
    public string? OverallAiComment { get; set; }
    public DateTime? OverallAiCommentGeneratedAt { get; set; }
}
