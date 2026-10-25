using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Planning;

public interface ILearningPathV4Planner
{
    Task<LearningGoalDto> CreateOrUpdateGoalAsync(
        Guid centerId,
        Guid studentId,
        CreateLearningGoalRequest request,
        CancellationToken cancellationToken = default);

    Task<LearningGoalDto?> GetActiveGoalAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task<PathProposalDto> GeneratePathProposalAsync(
        Guid centerId,
        Guid studentId,
        GeneratePathProposalRequest request,
        CancellationToken cancellationToken = default);

    Task<PathProposalDto> CustomizePathProposalAsync(
        Guid centerId, Guid studentId, Guid proposalId, CustomizePathProposalRequest request,
        CancellationToken cancellationToken = default);

    Task<ExtendPathCheckResponse> ExtendPathWithCheckAsync(Guid centerId, Guid studentId, ExtendPathCheckRequest request,
        CancellationToken cancellationToken = default);

    Task<AcceptPathProposalResponse> AcceptPathProposalAsync(
        Guid centerId,
        Guid studentId,
        Guid proposalId,
        CancellationToken cancellationToken = default);

    Task<CapabilityMapDto> GetCapabilityMapAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        Guid? classId = null,
        CancellationToken cancellationToken = default);

    Task<LearningAgendaDto> GetLearningAgendaAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        uint availableMinutes = 15,
        CancellationToken cancellationToken = default,
        Guid? classId = null);
}
