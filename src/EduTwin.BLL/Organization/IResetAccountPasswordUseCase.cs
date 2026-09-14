using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IResetAccountPasswordUseCase
{
    Task<ResetAccountPasswordResult> ExecuteAsync(
        Guid targetId,
        string targetRole,
        ResetAccountPasswordRequest request,
        string? traceId = null,
        CancellationToken cancellationToken = default);
}
