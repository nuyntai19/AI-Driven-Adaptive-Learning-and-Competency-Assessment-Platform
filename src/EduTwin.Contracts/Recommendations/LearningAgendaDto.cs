using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class LearningAgendaDto
{
    public decimal? TargetScore { get; set; }
    public decimal? PredictedScore { get; set; }
    public string ForecastMessage { get; set; } = "Chưa đủ dữ liệu để dự báo điểm.";
    public LearningScheduleSettings Schedule { get; set; } = new();
    public LearningScheduleSummaryDto? ScheduleSummary { get; set; }
    public List<ExamMilestoneDto> Exams { get; set; } = new();
    public List<string> SelfReportedPrerequisiteSkillIds { get; set; } = new();
    /// <summary>Minutes of work available today at the largest time budget; more time adds nothing.</summary>
    public uint AvailableTodayMinutes { get; set; }
    /// <summary>Why the first step of today's plan comes first.</summary>
    public string? PrimaryReason { get; set; }
    /// <summary>Parts the student left out of the placement check (self-assessed as mastered); each gets a short confirmation review at the end of the route.</summary>
    public List<string> SelfAssessedSkillIds { get; set; } = new();
    public bool IsPlacement { get; set; }
    public string GoalType { get; set; } = "Foundation";
    public DateTime? DeadlineDate { get; set; }
    public List<string> TargetSkillIds { get; set; } = new();
    public uint AvailableMinutesPerDay { get; set; } = 15;
    public uint StudiedMinutesToday { get; set; }
    public string PreferredPace { get; set; } = "Moderate";
    public uint AvailableDaysPerWeek { get; set; } = 5;
    public PlacementReportDto? PlacementReport { get; set; }
    public bool CanAdvance { get; set; }
    public List<string> NextSkillIds { get; set; } = new();
    public LearningInsightsDto Insights { get; set; } = new();
    public List<string> RecentChanges { get; set; } = new();
    public Guid? LearningPathId { get; set; }
    public string? GoalTitle { get; set; }
    public List<AgendaItemDto> Missions { get; set; } = new();
    public DateTime Date { get; set; }
    public uint AvailableMinutes { get; set; } = 15;
    public string AgendaMessage { get; set; } = string.Empty;

    public AgendaItemDto? PrimaryMission { get; set; }
    public List<AgendaItemDto> AlternativeOptions { get; set; } = new();
    public List<AgendaItemDto> OngoingRuns { get; set; } = new();
    public List<AgendaItemDto> ReviewDueTasks { get; set; } = new();

    /// <summary>Ordered steps that fit today's time budget; practice sets are resized to the budget.</summary>
    public List<AgendaItemDto> TodayPlan { get; set; } = new();
    public uint PlannedMinutes { get; set; }
    public uint PlannedQuestionCount { get; set; }
    public string? PacingNote { get; set; }
    /// <summary>Earlier routes for this subject. Answers are never deleted when a route is replaced.</summary>
    public List<PathHistoryItemDto> PathHistory { get; set; } = new();
    /// <summary>The active route was built before the practice loop; the student can rebuild it without losing answers.</summary>
    public bool PlanOutdated { get; set; }
    public GoalProgressDto? Progress { get; set; }
}

public class PlacementReportDto
{
    public uint TotalSkills { get; set; }
    public uint AssessedSkills { get; set; }
    public bool IsComplete { get; set; }
    public decimal? ScoreOutOfTen { get; set; }
    public List<PlacementSkillResultDto> Skills { get; set; } = new();
}

public class PlacementSkillResultDto
{
    public string Classification { get; set; } = "Inconclusive";
    public string Confidence { get; set; } = "Insufficient";
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public string TopicName { get; set; } = "";
    public uint AnsweredQuestions { get; set; }
    public uint CorrectAnswers { get; set; }
    public decimal EarnedScore { get; set; }
    public decimal PossibleScore { get; set; }
    public decimal? ScoreOutOfTen { get; set; }
    public string Status { get; set; } = "NotStarted";
    public string Recommendation { get; set; } = "";
    public uint EstimatedPracticeSets { get; set; }
}

public class GoalProgressDto
{
    public uint TotalSkills { get; set; }
    public uint ConfirmedSkills { get; set; }
    public uint ReadyForCheckSkills { get; set; }
    public uint RemainingMinutes { get; set; }
    public DateTime? EstimatedFinishDate { get; set; }
    public uint StreakDays { get; set; }
    public bool StudiedToday { get; set; }
}

public class PathHistoryItemDto
{
    public Guid LearningPathId { get; set; }
    public uint Version { get; set; }
    public string GoalTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public uint TotalMissions { get; set; }
    public uint CompletedMissions { get; set; }
    public uint AnsweredQuestions { get; set; }
}

public class MissionRunSummaryDto
{
    public ulong SessionRunId { get; set; }
    public uint RunNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public uint TotalQuestions { get; set; }
    public uint AnsweredQuestions { get; set; }
    public uint CorrectAnswers { get; set; }
}

public class AgendaItemDto
{
    public DateOnly? PlannedDate { get; set; }
    public bool Deferred { get; set; }
    public uint RemainingUnscheduledMinutes { get; set; }
    public string? SkipReason { get; set; }
    public bool IsMilestoneExam { get; set; }
    public bool IsMockExam { get; set; }
    public uint? TimeLimitMinutes { get; set; }
    public bool IsSelfAssessedCheck { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public uint QuestionCount { get; set; }
    public string MissionId { get; set; } = string.Empty;
    public string? LegacySessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty; // e.g. "Remedial", "CoreStep", "TransferChallenge", "Review"
    public string SkillId { get; set; } = string.Empty;
    public string SkillName { get; set; } = string.Empty;
    public uint EstimatedDurationMinutes { get; set; } = 15;
    public List<string> ActivityTypes { get; set; } = new();
    public string RecommendationReason { get; set; } = string.Empty;

    public bool CanStart { get; set; } = true;
    public string Status { get; set; } = "NotStarted"; // NotStarted, InProgress, Completed, NeedsReview
    public ulong? ActiveRunId { get; set; }
    public ulong? LatestRunId { get; set; }

    /// <summary>Planned size from the route (weakness based). QuestionCount may be resized for today's budget.</summary>
    public uint DefaultQuestionCount { get; set; }
    public string? WeaknessLevel { get; set; } // Weak, Developing, Strong
    public bool CanRetry { get; set; }
    public string? RetryLabel { get; set; }
    public string? RetryBlockedReason { get; set; }
    public List<MissionRunSummaryDto> Runs { get; set; } = new();
    /// <summary>Practice sets of this kind planned today; each set keeps a fixed size.</summary>
    public uint PlannedSets { get; set; } = 1;
    public uint SetNumber { get; set; }
    public uint EstimatedSets { get; set; }
    public string? Advice { get; set; }
}

public class LearningInsightsDto
{
    public uint WindowDays { get; set; } = 30;
    public uint AnswerCount { get; set; }
    public uint AssignmentCount { get; set; }
    public uint GradedQuestionCount { get; set; }
    public uint PendingCount { get; set; }
    public decimal? ScorePercentage { get; set; }
    public string Summary { get; set; } = "Chưa có bài làm gần đây. Hãy chọn mục tiêu để xác định điểm xuất phát.";
    public List<string> SuggestedSkillIds { get; set; } = new();
    public List<LearningSkillInsightDto> Skills { get; set; } = new();
}

public class LearningSkillInsightDto
{
    public string SkillId { get; set; } = "";
    public string SkillName { get; set; } = "";
    public uint QuestionCount { get; set; }
    public uint CorrectCount { get; set; }
    public uint WrongCount { get; set; }
    /// <summary>Correct answers given after opening a hint; they count as half evidence of strength.</summary>
    public uint HintedCorrectCount { get; set; }
    public string Observation { get; set; } = "";
    public List<string> ConfirmedMisconceptions { get; set; } = new();
    public List<LearningAnswerReferenceDto> RecentAnswers { get; set; } = new();
}

public class LearningAnswerReferenceDto
{
    public string AttemptId { get; set; } = "";
    public string QuestionText { get; set; } = "";
    public bool? IsCorrect { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool Guided { get; set; }
}
