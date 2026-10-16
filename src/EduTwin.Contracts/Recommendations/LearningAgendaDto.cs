using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class LearningAgendaDto
{
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
}

public class AgendaItemDto
{
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
