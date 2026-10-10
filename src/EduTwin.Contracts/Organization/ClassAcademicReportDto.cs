using EduTwin.Contracts.Assignments;

namespace EduTwin.Contracts.Organization;

public sealed class ClassAcademicReportDto
{
    public ClassDto Class { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public List<ClassStudentReportDto> Students { get; set; } = new();
    public int TotalAssignments { get; set; }
}

public sealed class ClassStudentReportDto
{
    public StudentDto Student { get; set; } = new();
    public StudentAcademicSummaryDto Summary { get; set; } = new();
}

public sealed class StudentAcademicSummaryDto
{
    public int TotalAssigned { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int NotStartedCount { get; set; }
    public int OverdueCount { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal? AverageScore { get; set; }
    public decimal? MinScore { get; set; }
    public decimal? MaxScore { get; set; }
    public string AssessmentStatus { get; set; } = "Unknown";
    public List<StudentAssignmentReportDto> Records { get; set; } = new();
}

public sealed class StudentAssignmentReportDto
{
    public Guid AssignmentId { get; set; }
    public string Title { get; set; } = "";
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public DateTime? DueAt { get; set; }
    public string Status { get; set; } = "NotStarted";
    public int CompletedQuestionCount { get; set; }
    public int TotalQuestionCount { get; set; }
    public decimal? Score { get; set; }
    public decimal MaxScore { get; set; } = 10;
    public string ResultStatus { get; set; } = "Processing";
    public string? FeedbackNote { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public sealed class ClassHistoryDto
{
    public List<ClassHistoryItemDto> Data { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
}

public sealed class ClassHistoryItemDto
{
    public string ActionType { get; set; } = "";
    public string ActorName { get; set; } = "";
    public Guid? ActorUserId { get; set; }
    public string Source { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public DateTime CreatedAt { get; set; }
}
