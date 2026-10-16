using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Services;

public interface ILearningActivityExecutionService
{
    Task<MissionRunDto> StartMissionAsync(Guid centerId, Guid studentId, Guid missionId, CancellationToken cancellationToken = default);
    Task<ActivityRunDto> GetActivityRunAsync(
        Guid centerId,
        Guid studentId,
        ulong activityRunId,
        CancellationToken cancellationToken = default);

    Task<SaveActivityDraftResponse> SaveDraftAsync(
        Guid centerId,
        Guid studentId,
        ulong activityRunId,
        SaveActivityDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<AssistanceResponseDto> RequestAssistanceAsync(
        Guid centerId,
        Guid studentId,
        ulong activityRunId,
        RequestAssistanceRequest request,
        CancellationToken cancellationToken = default);

    Task<ActivitySubmissionResponseDto> SubmitActivityAsync(
        Guid centerId,
        Guid studentId,
        ulong activityRunId,
        SubmitActivityRequest request,
        CancellationToken cancellationToken = default);
}
