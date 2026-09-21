using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public sealed class GenerateLearningPathRequest
{
    public Guid SubjectId { get; set; }
    public string SelfAssessedLevel { get; set; } = "Medium"; // VeryWeak, Weak, Medium, Good, Excellent
    public List<ulong> WeakTopicNodeIds { get; set; } = new();
    public List<ulong> FocusTopicNodeIds { get; set; } = new();
    public string GoalType { get; set; } = "Foundation"; // Foundation, KeepUp, ImproveGrade, ExamPrep, Advanced
    public decimal TargetMastery { get; set; } = 80m;
    public int TargetWeeks { get; set; } = 4;
    public int MinutesPerDay { get; set; } = 30;
    public int DaysPerWeek { get; set; } = 5;
    public string Pace { get; set; } = "Moderate"; // Gentle, Moderate, Accelerated
    public string PreferredMode { get; set; } = "Balanced"; // TheoryHeavy, PracticeHeavy, Balanced
    public string? Note { get; set; }
}

public sealed class StudentLearningPathPreferenceDto
{
    public Guid SubjectId { get; set; }
    public string SelfAssessedLevel { get; set; } = "Medium";
    public List<ulong> WeakTopicNodeIds { get; set; } = new();
    public List<ulong> FocusTopicNodeIds { get; set; } = new();
    public string GoalType { get; set; } = "Foundation";
    public decimal TargetMastery { get; set; } = 80m;
    public int TargetWeeks { get; set; } = 4;
    public int MinutesPerDay { get; set; } = 30;
    public int DaysPerWeek { get; set; } = 5;
    public string Pace { get; set; } = "Moderate";
    public string PreferredMode { get; set; } = "Balanced";
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class StudentLearningPathPreferenceResponse
{
    public StudentLearningPathPreferenceDto? Data { get; set; }
    public MetaDto Meta { get; set; } = null!;
}

public sealed class LearningPathPhaseDto
{
    public int PhaseNumber { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal ProgressPercentage { get; set; }
    public List<LearningPathWeekDto> Weeks { get; set; } = new();
}

public sealed class LearningPathWeekDto
{
    public int WeekNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public decimal ProgressPercentage { get; set; }
    public List<LearningPathSessionDto> Sessions { get; set; } = new();
    public LearningPathCheckpointDto Checkpoint { get; set; } = new();
}

public sealed class LearningPathSessionDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = "LearnTheory";
    public string TopicNodeId { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public List<string> OnlineActivities { get; set; } = new();
    public List<string> OfflineActivities { get; set; } = new();
    public List<string> RecommendedQuestionIds { get; set; } = new();
    public decimal RequiredCorrectRate { get; set; }
    public decimal TargetMastery { get; set; }
    public List<string> CompletionCriteria { get; set; } = new();
    public string Status { get; set; } = "NotStarted";
}

public sealed class LearningPathCheckpointDto
{
    public string Type { get; set; } = "Weekly";
    public decimal CurrentMastery { get; set; }
    public decimal TargetMastery { get; set; }
    public decimal RequiredCorrectRate { get; set; }
    public int CompletedTasks { get; set; }
    public List<string> WeakTopics { get; set; } = new();
    public string Recommendation { get; set; } = string.Empty;
}

public sealed class LearningPathItemDetailDto
{
    public string LearningPathItemId { get; set; } = string.Empty;
    public string TopicNodeId { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public decimal CurrentMastery { get; set; }
    public decimal TargetMastery { get; set; }
    public uint RankOrder { get; set; }
    public decimal? OpportunityScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Prerequisites { get; set; }
    public int EstimatedMinutes { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed
    public string? RecommendedQuestionId { get; set; }
}

public sealed class DetailedLearningPathDto
{
    public string LearningPathId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public uint Version { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime GeneratedAt { get; set; }
    public decimal CurrentOverallMastery { get; set; }
    public decimal TargetMastery { get; set; }
    public int EstimatedWeeks { get; set; }
    public int MinutesPerDay { get; set; }
    public int DaysPerWeek { get; set; }
    public decimal ProgressPercentage { get; set; }
    public string GoalType { get; set; } = "Foundation";
    public int TotalScheduledMinutes { get; set; }
    public int CapacityMinutes { get; set; }
    public string GenerationStatus { get; set; } = "Ready";
    public string? AdaptationMessage { get; set; }
    public LearningPathSessionDto? NextSession { get; set; }
    public string RecommendationRationale { get; set; } = string.Empty;
    public List<LearningPathPhaseDto> Phases { get; set; } = new();
}

public sealed class DetailedLearningPathResponse
{
    public DetailedLearningPathDto? Data { get; set; }
    public MetaDto Meta { get; set; } = null!;
}

public sealed class UpdateLearningPathSessionRequest
{
    public string Status { get; set; } = "InProgress";
}
