using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Recommendations.Services;

public record MaterializedSessionSummary(
    Guid SessionId,
    string? LegacySessionId,
    uint SessionOrder,
    string Title,
    LearningPathSessionStatus Status,
    int PracticeQuestionCount,
    int CheckpointQuestionCount
);

public record StartSessionRunResult(
    ulong SessionRunId,
    Guid SessionId,
    Guid StudentId,
    uint RunNumber,
    SessionRunStatus Status,
    SessionRunEvaluationStatus EvaluationStatus,
    ulong? CurrentQuestionId,
    ulong? CheckpointQuestionId,
    bool IsResumed
);

public record TransitionSessionRunRequest(
    SessionRunStatus TargetStatus,
    SessionRunEvaluationStatus? TargetEvaluationStatus = null,
    SessionRunOutcome? TargetOutcome = null,
    decimal? ScoreRate = null,
    uint? QuestionsAttempted = null,
    uint? QuestionsCorrect = null,
    decimal? EndingMastery = null
);

public class SessionStateTransitionException : InvalidOperationException
{
    public SessionStateTransitionException(string message) : base(message) { }
}

public class QuestionPoolDepletedException : InvalidOperationException
{
    public QuestionPoolDepletedException(string message) : base(message) { }
}

public record ResumeSessionRunResult(
    ulong? SessionRunId,
    Guid SessionId,
    string? LegacySessionId,
    string SessionTitle,
    LearningPathSessionStatus SessionStatus,
    uint? RunNumber,
    SessionRunStatus? RunStatus,
    SessionRunEvaluationStatus? EvaluationStatus,
    SessionRunOutcome? Outcome,
    DateTime? StartedAt,
    DateTime? LastActivityAt,
    DateTime? CompletedAt,
    uint TotalQuestions,
    uint QuestionsAttempted,
    uint QuestionsCorrect,
    decimal? ScoreRate,
    ulong? CurrentQuestionId,
    IReadOnlyList<ulong> AttemptedQuestionIds,
    bool IsCompleted,
    bool IsGrading,
    bool CanStart
);

public interface ILearningSessionExecutionService
{
    Task<IReadOnlyList<MaterializedSessionSummary>> MaterializeSessionsFromPlanAsync(
        Guid centerId,
        Guid learningPathId,
        CancellationToken ct = default);

    Task<StartSessionRunResult> StartOrResumeRunAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        CancellationToken ct = default);

    Task<StartSessionRunResult> StartOrResumeSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        string sessionIdOrLegacyId,
        Guid? classId = null,
        CancellationToken ct = default);

    Task<ResumeSessionRunResult> ResumeSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        string sessionIdOrLegacyId,
        Guid? classId = null,
        CancellationToken ct = default);

    Task<NextQuestionDto?> GetQuestionDtoAsync(
        Guid centerId,
        Guid sessionId,
        ulong questionId,
        bool isCheckpoint,
        CancellationToken ct = default);

    Task<LearningPathSessionRun> TransitionRunAsync(
        Guid centerId,
        ulong sessionRunId,
        TransitionSessionRunRequest request,
        CancellationToken ct = default);

    Task<LearningPathSession> TransitionSessionStatusAsync(
        Guid centerId,
        Guid sessionId,
        LearningPathSessionStatus targetStatus,
        CancellationToken ct = default);

    Task<EvaluateSessionRunResult> EvaluateSessionRunAsync(
        Guid centerId,
        ulong sessionRunId,
        CancellationToken ct = default);

    Task<EvaluateSessionRunResult> EvaluateSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        string sessionIdOrLegacyId,
        Guid? classId = null,
        ulong? sessionRunId = null,
        CancellationToken ct = default);
}

public record EvaluateSessionRunResult(
    ulong SessionRunId,
    Guid SessionId,
    string? LegacySessionId,
    string SessionTitle,
    LearningPathSessionStatus SessionStatus,
    uint RunNumber,
    SessionRunStatus RunStatus,
    SessionRunEvaluationStatus EvaluationStatus,
    SessionRunOutcome Outcome,
    uint TotalQuestions,
    uint QuestionsAttempted,
    uint CheckpointsAttempted,
    uint CheckpointsCorrect,
    decimal? CheckpointScoreRate,
    decimal? OverallScoreRate,
    bool RequiresTeacherReview,
    bool HasPendingAIGrading,
    string? EvaluationDetail,
    DateTime? EvaluatedAt,
    bool IsCompleted);
