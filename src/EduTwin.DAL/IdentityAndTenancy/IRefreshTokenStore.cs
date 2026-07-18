using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.DAL.IdentityAndTenancy;

public interface IRefreshTokenStore
{
    Task<Guid?> ResolveCenterIdAsync(string tokenHash, CancellationToken cancellationToken);

    Task<RefreshToken?> GetTokenAsync(string tokenHash, CancellationToken cancellationToken);

    Task<bool> RotateTokenAsync(
        ulong oldTokenId,
        string reason,
        string? clientIp,
        DateTime nowUtc,
        RefreshToken newToken,
        CancellationToken cancellationToken);

    Task<bool> RevokeTokenAsync(
        ulong tokenId,
        string reason,
        string? clientIp,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
