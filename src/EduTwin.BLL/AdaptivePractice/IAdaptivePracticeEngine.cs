using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.AdaptivePractice;

namespace EduTwin.BLL.AdaptivePractice;

public interface IAdaptivePracticeEngine
{
    Task<AdaptivePracticeProposalDto> GetProposalAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        ulong? customTopicId,
        AdaptivePracticeMode mode,
        CancellationToken ct);

    Task<AdaptivePracticeSessionDto> CreateSessionAsync(
        Guid centerId,
        Guid studentId,
        CreateAdaptivePracticeSessionRequest request,
        DateTime utcNow,
        CancellationToken ct);

    Task<AdaptivePracticeSessionDto?> GetActiveSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        CancellationToken ct);

    Task<AdaptivePracticeSessionDto?> GetSessionByIdAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        CancellationToken ct);

    Task<AdaptivePracticeSessionDto> ResumeSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct);

    Task<AdaptivePracticeSessionDto> PauseSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct);

    Task<AdaptivePracticeSummaryDto> CloseSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        AdaptivePracticeCloseReason closeReason,
        DateTime utcNow,
        CancellationToken ct);

    Task<AdaptivePracticeQuestionDto?> GetNextQuestionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct);

    Task<AdaptivePracticeHintDto> GetHintAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        ulong sessionItemId,
        int requestedLevel,
        CancellationToken ct);

    Task<AdaptivePracticeAnswerFeedbackDto> SubmitAnswerAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        SubmitAdaptivePracticeAnswerRequest request,
        DateTime utcNow,
        CancellationToken ct);

    Task<AdaptivePracticeSummaryDto?> GetSummaryAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        CancellationToken ct);

    Task<IReadOnlyList<AdaptivePracticeHistoryItemDto>> GetHistoryAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        int limit,
        CancellationToken ct);

    Task<IReadOnlyList<TeacherAdaptivePracticeSessionDto>> GetTeacherSessionsAsync(
        Guid centerId,
        Guid teacherId,
        Guid? classId,
        Guid? subjectId,
        int limit,
        CancellationToken ct);

    Task<IReadOnlyList<TeacherAdaptivePracticeAlertDto>> GetTeacherAlertsAsync(
        Guid centerId,
        Guid teacherId,
        Guid? classId,
        CancellationToken ct);
}
