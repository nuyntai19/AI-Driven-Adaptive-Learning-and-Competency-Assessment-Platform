using System;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public class EvaluateSessionDto
{
    public ulong SessionRunId { get; set; }
    public Guid SessionId { get; set; }
    public string? LegacySessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public LearningPathSessionStatus SessionStatus { get; set; }
    public uint RunNumber { get; set; }
    public SessionRunStatus RunStatus { get; set; }
    public SessionRunEvaluationStatus EvaluationStatus { get; set; }
    public SessionRunOutcome Outcome { get; set; }
    public uint TotalQuestions { get; set; }
    public uint QuestionsAttempted { get; set; }
    public uint CheckpointsAttempted { get; set; }
    public uint CheckpointsCorrect { get; set; }
    public decimal? CheckpointScoreRate { get; set; }
    public decimal? OverallScoreRate { get; set; }
    public bool RequiresTeacherReview { get; set; }
    public bool HasPendingAIGrading { get; set; }
    public string? EvaluationDetail { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public bool IsCompleted { get; set; }
    public bool Passed { get; set; }
}

public class EvaluateSessionResponse
{
    public bool IsSuccess { get; set; } = true;
    public EvaluateSessionDto? Data { get; set; }
    public MetaDto? Meta { get; set; }
}
