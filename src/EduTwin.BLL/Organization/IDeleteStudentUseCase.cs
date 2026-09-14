using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IDeleteStudentUseCase
{
    Task<DeleteStudentResult> ExecuteAsync(Guid studentId, string? traceId = null, CancellationToken cancellationToken = default);
}
