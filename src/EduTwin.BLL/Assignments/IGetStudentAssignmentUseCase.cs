using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.Assignments;

public interface IGetStudentAssignmentUseCase
{
    Task<GetStudentAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken);
}
