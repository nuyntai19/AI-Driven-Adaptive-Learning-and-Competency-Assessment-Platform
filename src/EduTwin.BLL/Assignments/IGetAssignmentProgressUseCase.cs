using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Assignments;

public interface IGetAssignmentProgressUseCase
{
    Task<GetAssignmentProgressResult> ExecuteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default);
}
