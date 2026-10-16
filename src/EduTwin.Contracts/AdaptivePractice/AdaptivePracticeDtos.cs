using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.Contracts.AdaptivePractice;

public sealed class AdaptivePracticeTopicOptionDto
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TopicNodeId { get; set; }
    public string TopicName { get; set; } = null!;
    public decimal CurrentMastery { get; set; }
    public bool HasPrerequisites { get; set; }
}

public sealed class AdaptivePracticeProposalDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TopicNodeId { get; set; }
    public string TopicName { get; set; } = null!;
    public string FocusSkillDescription { get; set; } = null!;
    public decimal CurrentMastery { get; set; }
    public decimal TargetMastery { get; set; }
    public string SelectionReason { get; set; } = null!;
    public int RecommendedTimeMinutes { get; set; } = 15;
    public bool IsDiagnosticRecommended { get; set; }
    public AdaptivePracticeMode DefaultMode { get; set; } = AdaptivePracticeMode.Weakness;
    public IReadOnlyList<AdaptivePracticeTopicOptionDto> AvailableTopics { get; set; } = [];
}

public sealed class CreateAdaptivePracticeSessionRequest
{
    public Guid SubjectId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TopicNodeId { get; set; }

    public AdaptivePracticeMode PracticeMode { get; set; } = AdaptivePracticeMode.Weakness;
    public int TimeBudgetMinutes { get; set; } = 15;
    public decimal? CustomTargetMastery { get; set; }
    public Guid? LearningPathId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? LearningPathItemId { get; set; }
}

public sealed class AdaptivePracticeSessionDto
{
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TopicNodeId { get; set; }
    public string TopicName { get; set; } = null!;

    public AdaptivePracticeMode PracticeMode { get; set; }
    public decimal StartingMastery { get; set; }
    public decimal TargetMastery { get; set; }
    public decimal CurrentMastery { get; set; }
    public int TimeBudgetMinutes { get; set; }
    public int MinimumQuestions { get; set; }
    public int MaximumQuestions { get; set; }
    public int QuestionCount { get; set; }
    public AdaptivePracticeSessionStatus Status { get; set; }
    public AdaptivePracticeCloseReason? CloseReason { get; set; }
    public string SelectionReason { get; set; } = null!;
    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ElapsedSeconds { get; set; }
    public bool IsDiagnosticMode { get; set; }
    public byte CurrentDifficulty { get; set; }
}

public sealed class AdaptivePracticeQuestionDto
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong SessionItemId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong QuestionId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TopicNodeId { get; set; }
    public string TopicName { get; set; } = null!;

    public byte Difficulty { get; set; }
    public string QuestionText { get; set; } = null!;
    public QuestionType QuestionType { get; set; }
    public IReadOnlyList<StudentQuestionOptionDto> Options { get; set; } = [];
    public bool HasImage { get; set; }
    public decimal MaxScore { get; set; }
    public uint EstimatedTimeSeconds { get; set; }
    public string SelectionReasonCode { get; set; } = null!;
    public string SelectionReasonText { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public int TotalQuestionsEstimatedMin { get; set; }
    public int TotalQuestionsEstimatedMax { get; set; }
    public int AvailableHintsCount { get; set; } = 3;
    public int HintsUsedCount { get; set; } = 0;
    public bool IsPrerequisiteCheck { get; set; }
}

public sealed class AdaptivePracticeHintDto
{
    public int HintLevel { get; set; }
    public string HintTitle { get; set; } = null!;
    public string HintContent { get; set; } = null!;
    public string SupportLevelDescription { get; set; } = null!;
}

public sealed class SubmitAdaptivePracticeAnswerRequest
{
    public Guid ClientSubmissionId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong QuestionId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong SessionItemId { get; set; }

    public string FinalAnswer { get; set; } = null!;
    public string? ReasoningText { get; set; }
    public decimal? StudentConfidence { get; set; }
    public uint TimeSpentSeconds { get; set; }
}

public sealed class AdaptivePracticeSummaryDto
{
    public Guid SessionId { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int PartialCount { get; set; }
    public int PendingCount { get; set; }
    public int TimeSpentSeconds { get; set; }
    public decimal StartingMastery { get; set; }
    public decimal? EndingMastery { get; set; }
    public string GoalAchievementStatus { get; set; } = null!;
    public IReadOnlyList<string> StrongSkills { get; set; } = [];
    public IReadOnlyList<string> WeakSkills { get; set; } = [];
    public IReadOnlyList<string> RemediationSkills { get; set; } = [];
    public string SummaryConclusion { get; set; } = null!;
    public string RecommendedNextAction { get; set; } = null!;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? RecommendedNextTopicId { get; set; }
    public string? RecommendedNextTopicName { get; set; }
}

public sealed class AdaptivePracticeAnswerFeedbackDto
{
    public bool IsCorrect { get; set; }
    public decimal AwardedScore { get; set; }
    public string CorrectAnswer { get; set; } = null!;
    public string SolutionExplanation { get; set; } = null!;
    public string? StepGuidance { get; set; }
    public int HintsUsed { get; set; }
    public string? MisconceptionHypothesis { get; set; }
    public string NextActionRecommendation { get; set; } = null!;
    public bool IsSessionClosed { get; set; }
    public AdaptivePracticeCloseReason? CloseReason { get; set; }
    public AdaptivePracticeSummaryDto? SessionSummary { get; set; }
    public decimal UpdatedEstimatedMastery { get; set; }
}

public sealed class AdaptivePracticeHistoryItemDto
{
    public Guid SessionId { get; set; }
    public string SubjectName { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public AdaptivePracticeMode PracticeMode { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public decimal StartingMastery { get; set; }
    public decimal? EndingMastery { get; set; }
    public AdaptivePracticeSessionStatus Status { get; set; }
    public AdaptivePracticeCloseReason? CloseReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TimeSpentSeconds { get; set; }
}

public sealed class TeacherAdaptivePracticeSessionDto
{
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public int TotalQuestions { get; set; }
    public int CorrectCount { get; set; }
    public string GoalAchievementStatus { get; set; } = null!;
    public AdaptivePracticeCloseReason? CloseReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool RequiresIntervention { get; set; }
    public string? InterventionReason { get; set; }
}

public sealed class TeacherAdaptivePracticeAlertDto
{
    public string AlertId { get; set; } = null!;
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TopicNodeId { get; set; }
    public string TopicName { get; set; } = null!;

    public string AlertType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
