using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public interface ICloseAssignmentUseCase
{
    Task<CloseAssignmentResult> ExecuteAsync(Guid assignmentId, CloseAssignmentRequest request, CancellationToken cancellationToken = default);
}
