namespace EduTwin.Contracts.Recommendations;

public class LearningScheduleSettings
{
    public Guid? SourceGoalId { get; set; }
    public string? SourceGoalVersion { get; set; }
    // DayOfWeek: Sunday = 0. Empty means Monday onward, for AvailableDaysPerWeek days.
    public List<int> StudyDays { get; set; } = new();
    public List<DateOnly> ExcludedDates { get; set; } = new();
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public uint ReviewReservePercent { get; set; } = 20;
    public uint ShortCheckDays { get; set; } = 45;
    public uint MockExamDays { get; set; } = 14;
    public uint QuestionReuseDelayDays { get; set; } = 2;
}

public class StudySlotDto
{
    public DateOnly Date { get; set; }
    public uint Minutes { get; set; }
}

public class LearningScheduleSummaryDto
{
    public string? SourceGoalVersion { get; set; }
    public int Version { get; set; } = 1;
    public DateOnly CalculatedFrom { get; set; }
    public DateOnly? Deadline { get; set; }
    public uint CapacityMinutes { get; set; }
    public uint RequiredMinutes { get; set; }
    public uint ReserveMinutes { get; set; }
    public uint MissingMinutes { get; set; }
    public string Phase { get; set; } = "Balanced";
    public List<string> DeferredSkillIds { get; set; } = new();
    public string Message { get; set; } = "";
}

public class ExamMilestoneDto
{
    public Guid AssignmentId { get; set; }
    public Guid ClassId { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "Regular";
    public DateTime ExamAt { get; set; }
    public int TimeLimitMinutes { get; set; }
    public string Status { get; set; } = "Scheduled";
    public List<string> SupportSkillIds { get; set; } = new();
}
