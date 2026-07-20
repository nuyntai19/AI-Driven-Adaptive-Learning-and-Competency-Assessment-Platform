using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IRemoveStudentFromClassUseCase
{
    Task<RemoveStudentFromClassResult> ExecuteAsync(
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken = default);
}
