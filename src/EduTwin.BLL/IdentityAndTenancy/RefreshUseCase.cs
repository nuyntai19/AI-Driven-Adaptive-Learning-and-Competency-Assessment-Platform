using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.IdentityAndTenancy;

public class RefreshUseCase : IRefreshUseCase
{
    private readonly IRefreshTokenCodec _codec;
    private readonly IRefreshTokenStore _store;
    private readonly IBackgroundTenantScopeFactory _scopeFactory;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly EduTwinDbContext _dbContext;

    public RefreshUseCase(
        IRefreshTokenCodec codec,
        IRefreshTokenStore store,
        IBackgroundTenantScopeFactory scopeFactory,
        IJwtTokenGenerator jwtTokenGenerator,
        TimeProvider timeProvider,
        EduTwinDbContext dbContext)
    {
        _codec = codec;
        _store = store;
        _scopeFactory = scopeFactory;
        _jwtTokenGenerator = jwtTokenGenerator;
        _timeProvider = timeProvider;
        _dbContext = dbContext;
    }

    public async Task<LoginResult> ExecuteAsync(string? rawToken, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (rawToken is null || !_codec.IsValidRawToken(rawToken))
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthRefreshInvalid };
        }

        var tokenHash = _codec.HashToken(rawToken);

        var centerId = await _store.ResolveCenterIdAsync(tokenHash, cancellationToken);
        if (!centerId.HasValue)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthRefreshInvalid };
        }

        using var scope = _scopeFactory.BeginScope(centerId.Value);

        var oldToken = await _store.GetTokenAsync(tokenHash, cancellationToken);
        if (oldToken == null)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthRefreshInvalid };
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        if (oldToken.ExpiresAt <= nowUtc ||
            oldToken.RevokedAt != null ||
            oldToken.ReplacedByTokenId != null)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthRefreshInvalid };
        }

        if (oldToken.CenterId != centerId.Value || oldToken.User?.CenterId != centerId.Value)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthRefreshInvalid };
        }

        var user = oldToken.User;
        if (user == null || user.IsDeleted || user.Status == UserStatus.Locked || user.Status == UserStatus.Disabled)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthUserDisabled };
        }

        var center = await _dbContext.Centers.FirstOrDefaultAsync(c => c.CenterId == centerId.Value, cancellationToken);
        if (center == null || center.Status == CenterStatus.Suspended || center.IsDeleted)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthUserDisabled };
        }

        // Generate Access Token first, if it fails it won't persist anything
        var accessToken = _jwtTokenGenerator.GenerateToken(user, center.CenterId);

        // Generate new Refresh Token
        var newRawToken = _codec.GenerateRawToken();
        var newHash = _codec.HashToken(newRawToken);

        var newToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = newHash,
            ExpiresAt = nowUtc.AddDays(30),
            CenterId = center.CenterId,
            CreatedAt = nowUtc,
            CreatedBy = user.UserId,
            CreatedByIp = clientIp
        };

        var rotated = await _store.RotateTokenAsync(
            oldToken.RefreshTokenId,
            "Rotated",
            clientIp,
            nowUtc,
            newToken,
            cancellationToken);

        if (!rotated)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthRefreshInvalid };
        }

        var data = new LoginDataDto
        {
            AccessToken = accessToken,
            User = new UserDto
            {
                UserId = user.UserId.ToString("D").ToLowerInvariant(),
                CenterId = center.CenterId.ToString("D").ToLowerInvariant(),
                CenterName = center.CenterName,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.RoleName.ToString()
            }
        };

        return new LoginResult
        {
            IsSuccess = true,
            Data = data,
            RawRefreshToken = newRawToken,
            RefreshTokenExpiresAt = newToken.ExpiresAt
        };
    }
}
