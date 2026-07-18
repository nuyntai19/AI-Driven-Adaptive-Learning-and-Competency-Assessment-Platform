using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.IdentityAndTenancy;

public class LoginUseCase : ILoginUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly IBackgroundTenantScopeFactory _scopeFactory;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly IRefreshTokenCodec _refreshTokenCodec;

    public LoginUseCase(
        EduTwinDbContext dbContext,
        IBackgroundTenantScopeFactory scopeFactory,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        TimeProvider timeProvider,
        IRefreshTokenCodec refreshTokenCodec)
    {
        _dbContext = dbContext;
        _scopeFactory = scopeFactory;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _timeProvider = timeProvider;
        _refreshTokenCodec = refreshTokenCodec;
    }

    public async Task<LoginResult> ExecuteAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        // 1. Resolve Center without global query filter (but exclude soft-deleted centers)
        var center = await _dbContext.Centers
            .FirstOrDefaultAsync(c => c.CenterCode == request.CenterCode && !c.IsDeleted, cancellationToken);

        if (center == null)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthInvalidCredentials };
        }

        // 2. Initialize Tenant Context Scope
        using var scope = _scopeFactory.BeginScope(center.CenterId);

        // 3. Find User (Soft deleted users are excluded by Global Query Filter)
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user == null)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthInvalidCredentials };
        }

        // 4. Verify Password
        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthInvalidCredentials };
        }

        // 5. Check Disabled semantics
        if (center.Status == CenterStatus.Suspended ||
            user.Status == UserStatus.Locked ||
            user.Status == UserStatus.Disabled)
        {
            return new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthUserDisabled };
        }

        // 6. Generate Refresh Token
        var rawRefreshToken = _refreshTokenCodec.GenerateRawToken();
        var refreshTokenHash = _refreshTokenCodec.HashToken(rawRefreshToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var refreshToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshTokenHash,
            ExpiresAt = now.AddDays(30),
            CenterId = center.CenterId,
            CreatedAt = now,
            CreatedBy = user.UserId,
            CreatedByIp = clientIp
        };

        _dbContext.RefreshTokens.Add(refreshToken);

        user.LastLoginAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = user.UserId;

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        // 7. Generate Access Token (if this fails, it throws and doesn't reach SaveChanges)
        var accessToken = _jwtTokenGenerator.GenerateToken(user, center.CenterId);

        // 8. Save Changes (only one atomic save)
        await _dbContext.SaveChangesAsync(cancellationToken);

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
            RawRefreshToken = rawRefreshToken,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt
        };
    }
}
