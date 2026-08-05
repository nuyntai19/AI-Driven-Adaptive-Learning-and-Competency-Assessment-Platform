using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

/// <summary>
/// Use case: POST /api/v1/assignments/{id}/publish (API_CONTRACTS.md §51).
/// Materiallize Targets và Progress; chốt Assignment từ Draft → Published.
/// </summary>
public interface IPublishAssignmentUseCase
{
    Task<PublishAssignmentResult> ExecuteAsync(
        Guid assignmentId,
        PublishAssignmentRequest request,
        CancellationToken cancellationToken = default);
}
