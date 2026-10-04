using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IDeleteClassUseCase
{
    Task<DeleteClassResult> ExecuteAsync(
        Guid classId,
        uint? expectedRowVersion = null,
        string? traceId = null,
        CancellationToken cancellationToken = default);
}
