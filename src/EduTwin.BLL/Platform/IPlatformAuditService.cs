using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Platform;

namespace EduTwin.BLL.Platform;

public interface IPlatformAuditService
{
    Task<PlatformResult<PlatformAuditListData>> ListAuditLogsAsync(
        PlatformAuditQuery query,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<PlatformAuditItemDto>> GetAuditLogByIdAsync(
        ulong auditId,
        CancellationToken cancellationToken = default);
}
