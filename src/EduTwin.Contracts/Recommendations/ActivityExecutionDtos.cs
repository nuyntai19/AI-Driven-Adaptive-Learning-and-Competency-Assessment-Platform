using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Recommendations;

public class ActivityRunDto
{
    public EduTwin.Contracts.Recommendations.NextQuestionDto? Question { get; set; }
    public ulong ActivityRunId { get; set; }
    public ulong SessionRunId { get; set; }
    public uint ActivityOrder { get; set; }
    public string ActivityType { get; set; } = "WorkedExample"; // WorkedExample, FindError, IndependentCheck, Explore, TransferChallenge
    public string Status { get; set; } = "NotStarted";

    public string? DraftPayload { get; set; }
    public uint DraftRevision { get; set; } = 1;

    public uint AssistanceRequestedCount { get; set; }
    public string AssistanceMode { get; set; } = "Normal";

    public decimal? Score { get; set; }
    public decimal? MaxScore { get; set; }
    public bool? IsMastered { get; set; }
    public string? Feedback { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ActivityContentDto? Content { get; set; }
}

public class ActivityContentDto
{
    public Guid ContentVersionId { get; set; }
    public string SkillId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public uint EstimatedDurationMinutes { get; set; } = 15;
    public object? Payload { get; set; }
    public object? Rubric { get; set; }
}

public class SaveActivityDraftRequest
{
    public string DraftPayload { get; set; } = string.Empty;
    public uint ExpectedRevision { get; set; } = 1;
}

public class SaveActivityDraftResponse
{
    public ulong ActivityRunId { get; set; }
    public uint DraftRevision { get; set; }
    public DateTime SavedAt { get; set; }
    public bool HasConflict { get; set; }
}

public class RequestAssistanceRequest
{
    public uint StepIndex { get; set; }
    public string? StudentQuery { get; set; }
    public ulong? QuestionId { get; set; }
}

public class AssistanceResponseDto
{
    public string AssistanceMode { get; set; } = "Normal"; // Normal, Blocked, HintOnly
    public string Explanation { get; set; } = string.Empty;
    public string? Hint { get; set; }
    public bool FallbackUsed { get; set; }
    public bool IsBlocked { get; set; }
    public string PolicyNotice { get; set; } = string.Empty;
}

public class SubmitActivityRequest
{
    public Guid ClientSubmissionId { get; set; }
    [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
    public ulong? QuestionId { get; set; }
    public string FinalAnswer { get; set; } = string.Empty;
    public string? ReasoningText { get; set; }
    public string? AnswerDisplayLatex { get; set; }
    public int TimeSpentSeconds { get; set; } = 30;
    public string? DrawingUploadToken { get; set; }
    public string? SelectedErrorStep { get; set; }
    public string? MisconceptionReason { get; set; }
}

public class ActivitySubmissionResponseDto
{
    public ulong ActivityRunId { get; set; }
    public ulong? AttemptId { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? AwardedScore { get; set; }
    public string ActivityStatus { get; set; } = "Completed";
    public string OutcomeFeedback { get; set; } = string.Empty;
    public uint NextActivityOrder { get; set; }
    public bool IsRunFinished { get; set; }
}

public class V4SessionEvaluationResultDto
{
    public ulong SessionRunId { get; set; }
    public Guid SessionId { get; set; }
    public string Outcome { get; set; } = "InsufficientEvidence";
    public uint TotalActivities { get; set; }
    public uint CompletedActivities { get; set; }

    public List<string> ProvenSkills { get; set; } = new();
    public List<string> SkillsNeedingSupport { get; set; } = new();
    public List<string> UnmeasuredSkills { get; set; } = new();

    public List<string> DecisionReasons { get; set; } = new();
    public List<ProposedMissionDto> NextActions { get; set; } = new();

    public bool HasPendingReview { get; set; }
    public DateTime EvaluatedAt { get; set; }
}

public class MissionRunDto
{
    public ulong SessionRunId { get; set; }
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<ActivityRunDto> Activities { get; set; } = new();
}
