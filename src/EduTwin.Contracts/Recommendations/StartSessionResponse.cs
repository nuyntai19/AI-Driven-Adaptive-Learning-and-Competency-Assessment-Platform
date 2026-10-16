using System;
using System.Text.Json.Serialization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public class StartSessionDto
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong SessionRunId { get; set; }

    public Guid SessionId { get; set; }
    public string? LegacySessionId { get; set; }
    public uint RunNumber { get; set; }
    public SessionRunStatus Status { get; set; }
    public SessionRunEvaluationStatus EvaluationStatus { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? CurrentQuestionId { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? CheckpointQuestionId { get; set; }

    public bool IsResumed { get; set; }
    public uint TotalQuestions { get; set; }
    public uint QuestionsAttempted { get; set; }
    public NextQuestionDto? CurrentQuestion { get; set; }
}

public class StartSessionResponse
{
    public StartSessionDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
