using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public interface IUpdateAssignmentUseCase
{
    Task<UpdateAssignmentResult> ExecuteAsync(Guid assignmentId, UpdateAssignmentRequest request, CancellationToken cancellationToken = default);
}
