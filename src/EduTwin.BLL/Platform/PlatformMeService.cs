using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Platform;

public sealed class PlatformMeService : IPlatformMeService
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public PlatformMeService(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IPasswordHasher<User> passwordHasher,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    private bool IsAuthorizedPlatformAdmin(out Guid callerUserId)
    {
        callerUserId = Guid.Empty;
        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId != AuthorizationBootstrapper.ReservedPlatformCenterId ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.PlatformAdmin), StringComparison.Ordinal) ||
            !_tenantContext.UserId.HasValue)
        {
            return false;
        }

        callerUserId = _tenantContext.UserId.Value;
        return true;
    }

    public async Task<PlatformResult<PlatformSecurityProfileDto>> GetSecurityProfileAsync(
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformAdmin(out var callerUserId))
        {
            return PlatformResult<PlatformSecurityProfileDto>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền truy cập thông tin bảo mật tài khoản.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                u => u.UserId == callerUserId &&
                     u.RoleName == UserRole.PlatformAdmin,
                cancellationToken);

        if (user is null)
        {
            return PlatformResult<PlatformSecurityProfileDto>.Failure(
                ErrorCodes.ResourceNotFound, "Không tìm thấy hồ sơ quản trị viên nền tảng hợp lệ.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var activeSessionCount = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == callerUserId &&
                         rt.RevokedAt == null &&
                         rt.ExpiresAt > now)
            .CountAsync(cancellationToken);

        return PlatformResult<PlatformSecurityProfileDto>.Success(new PlatformSecurityProfileDto
        {
            UserId = user.UserId,
            Username = user.Username,
            DisplayName = user.DisplayName,
            RoleName = user.RoleName.ToString(),
            LastLoginAt = user.LastLoginAt,
            AuthVersion = user.AuthVersion,
            RowVersion = user.RowVersion.ToString(CultureInfo.InvariantCulture),
            ActiveSessionCount = activeSessionCount
        });
    }

    public async Task<PlatformResult<bool>> ChangePasswordAsync(
        PlatformChangePasswordRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformAdmin(out var callerUserId))
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền đổi mật khẩu tài khoản quản trị.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ValidationFailed, "Vui lòng nhập mật khẩu hiện tại.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ValidationFailed, "Mật khẩu mới phải có tối thiểu 12 ký tự.");
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ValidationFailed, "Xác nhận mật khẩu mới không khớp.");
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ValidationFailed, "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                u => u.UserId == callerUserId &&
                     u.RoleName == UserRole.PlatformAdmin,
                cancellationToken);

        if (user is null)
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ResourceNotFound, "Không tìm thấy hồ sơ quản trị viên nền tảng hợp lệ.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.AuthInvalidCredentials, "Mật khẩu hiện tại không chính xác.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.AuthVersion++;
        user.RowVersion++;
        user.UpdatedAt = now;
        user.UpdatedBy = callerUserId;

        // Bulk revoke active refresh tokens for the platform admin
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == callerUserId &&
                         rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokeReason = "Password changed by platform administrator.";
        }

        var auditLog = new AuthorizationAuditLog
        {
            CenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            TargetCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            ActorUserId = callerUserId,
            TargetUserId = null,
            TargetType = "User",
            TargetId = callerUserId.ToString("D"),
            ActionType = "PlatformAdminPasswordChanged",
            BeforeData = null, // Strictly zero credentials logged
            AfterData = JsonSerializer.Serialize(new
            {
                AuthVersion = user.AuthVersion,
                RevokedTokenCount = activeTokens.Count
            }),
            Reason = "Platform administrator changed personal account password.",
            TraceId = string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId,
            CreatedAt = now,
            CreatedBy = callerUserId
        };

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        _dbContext.AuthorizationAuditLogs.Add(auditLog);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng đã bị thay đổi bởi thao tác khác.");
        }

        return PlatformResult<bool>.Success(true);
    }

    public async Task<PlatformResult<bool>> RevokeSessionsAsync(
        PlatformRevokeSessionsRequest? request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformAdmin(out var callerUserId))
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền thu hồi phiên đăng nhập tài khoản quản trị.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(
                u => u.UserId == callerUserId &&
                     u.RoleName == UserRole.PlatformAdmin,
                cancellationToken);

        if (user is null)
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ResourceNotFound, "Không tìm thấy hồ sơ quản trị viên nền tảng hợp lệ.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.AuthVersion++;
        user.RowVersion++;
        user.UpdatedAt = now;
        user.UpdatedBy = callerUserId;

        var reason = string.IsNullOrWhiteSpace(request?.Reason)
            ? "Platform administrator manually revoked all active sessions."
            : request.Reason.Trim();

        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == callerUserId &&
                         rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokeReason = reason;
        }

        var auditLog = new AuthorizationAuditLog
        {
            CenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            TargetCenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            ActorUserId = callerUserId,
            TargetUserId = null,
            TargetType = "User",
            TargetId = callerUserId.ToString("D"),
            ActionType = "PlatformAdminSessionsRevoked",
            BeforeData = null,
            AfterData = JsonSerializer.Serialize(new
            {
                AuthVersion = user.AuthVersion,
                RevokedTokenCount = activeTokens.Count
            }),
            Reason = reason,
            TraceId = string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId,
            CreatedAt = now,
            CreatedBy = callerUserId
        };

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        _dbContext.AuthorizationAuditLogs.Add(auditLog);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return PlatformResult<bool>.Failure(
                ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng đã bị thay đổi bởi thao tác khác.");
        }

        return PlatformResult<bool>.Success(true);
    }
}
