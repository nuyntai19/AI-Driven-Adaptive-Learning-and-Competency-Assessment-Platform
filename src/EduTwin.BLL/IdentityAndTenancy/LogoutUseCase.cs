using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public class LogoutUseCase : ILogoutUseCase
{
    private readonly IRefreshTokenCodec _codec;
    private readonly IRefreshTokenStore _store;
    private readonly IBackgroundTenantScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public LogoutUseCase(
        IRefreshTokenCodec codec,
        IRefreshTokenStore store,
        IBackgroundTenantScopeFactory scopeFactory,
        TimeProvider timeProvider)
    {
        _codec = codec;
        _store = store;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
    }

    public async Task ExecuteAsync(string? rawToken, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (rawToken is null || !_codec.IsValidRawToken(rawToken))
        {
            return;
        }

        var tokenHash = _codec.HashToken(rawToken);

        var centerId = await _store.ResolveCenterIdAsync(tokenHash, cancellationToken);
        if (!centerId.HasValue)
        {
            return;
        }

        using var scope = _scopeFactory.BeginScope(centerId.Value);

        var token = await _store.GetTokenAsync(tokenHash, cancellationToken);
        if (token == null || token.RevokedAt != null)
        {
            return; // Already revoked or doesn't exist
        }

        if (token.CenterId != centerId.Value || token.User?.CenterId != centerId.Value)
        {
            return; // Cross-tenant mismatch
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _store.RevokeTokenAsync(token.RefreshTokenId, "Logout", clientIp, nowUtc, cancellationToken);
    }
}
