namespace EduTwin.Contracts.Assignments;

public sealed class AssignmentFinalReviewEligibilityDto
{
    public bool CanApprove { get; set; }
    public int MissingQuestionCount { get; set; }
    public int PendingReviewQuestionCount { get; set; }
    public int ProcessingQuestionCount { get; set; }
    public int FailedQuestionCount { get; set; }
    public string? BlockReason { get; set; }
}
