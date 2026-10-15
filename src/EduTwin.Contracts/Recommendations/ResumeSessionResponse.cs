using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public class ResumeSessionDto
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? SessionRunId { get; set; }

    public Guid SessionId { get; set; }
    public string? LegacySessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public LearningPathSessionStatus SessionStatus { get; set; }

    public uint? RunNumber { get; set; }
    public SessionRunStatus? RunStatus { get; set; }
    public SessionRunEvaluationStatus? EvaluationStatus { get; set; }
    public SessionRunOutcome? Outcome { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public uint TotalQuestions { get; set; }
    public uint QuestionsAttempted { get; set; }
    public uint QuestionsCorrect { get; set; }
    public decimal? ScoreRate { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? CurrentQuestionId { get; set; }

    public NextQuestionDto? CurrentQuestion { get; set; }
    public IReadOnlyList<ulong> AttemptedQuestionIds { get; set; } = [];

    public bool IsCompleted { get; set; }
    public bool IsGrading { get; set; }
    public bool CanStart { get; set; }
}

public class ResumeSessionResponse
{
    public ResumeSessionDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
