using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Assignments;

public interface IGetAssignmentUseCase
{
    Task<GetAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}
