namespace EduTwin.Contracts.Assignments;

public class AssignmentProgressItemDto
{
    public string StudentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CompletedQuestionCount { get; set; }
    public int TotalQuestionCount { get; set; }
    public string TeacherFinalReviewStatus { get; set; } = "Pending";
    public uint FinalReviewVersion { get; set; }
    public DateTime? CompletedAt { get; set; }
}
