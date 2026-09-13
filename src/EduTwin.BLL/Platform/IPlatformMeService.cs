using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Platform;

namespace EduTwin.BLL.Platform;

public interface IPlatformMeService
{
    Task<PlatformResult<PlatformSecurityProfileDto>> GetSecurityProfileAsync(
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<bool>> ChangePasswordAsync(
        PlatformChangePasswordRequest request,
        string traceId,
        CancellationToken cancellationToken = default);

    Task<PlatformResult<bool>> RevokeSessionsAsync(
        PlatformRevokeSessionsRequest? request,
        string traceId,
        CancellationToken cancellationToken = default);
}
