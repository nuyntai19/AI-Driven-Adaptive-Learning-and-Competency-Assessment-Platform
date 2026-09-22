using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public interface IStartStudentAssignmentUseCase
{
    Task<GetStudentAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}
