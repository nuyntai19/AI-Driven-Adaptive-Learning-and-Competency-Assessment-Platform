using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.Organization;

public class ResetAccountPasswordUseCase : IResetAccountPasswordUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ResetAccountPasswordUseCase> _logger;

    public ResetAccountPasswordUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IPasswordHasher<User> passwordHasher,
        TimeProvider timeProvider,
        ILogger<ResetAccountPasswordUseCase> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ResetAccountPasswordResult> ExecuteAsync(
        Guid targetId,
        string targetRole,
        ResetAccountPasswordRequest request,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal) ||
            targetId == Guid.Empty)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!string.Equals(targetRole, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
            !string.Equals(targetRole, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (request == null)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ValidationFailed, "Yêu cầu đặt lại mật khẩu không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12 || request.NewPassword.Length > 200)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ValidationFailed, "Mật khẩu mới phải từ 12 đến 200 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedUserRowVersion) ||
            !ulong.TryParse(request.ExpectedUserRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion) ||
            expectedVersion == 0)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ValidationFailed, "ExpectedUserRowVersion không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5 || request.Reason.Trim().Length > 500)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ValidationFailed, "Lý do đặt lại mật khẩu là bắt buộc (từ 5 đến 500 ký tự).");
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ResourceNotFound);
        }

        User? user = null;

        if (string.Equals(targetRole, nameof(UserRole.Teacher), StringComparison.Ordinal))
        {
            var teacher = await _dbContext.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                    t.TeacherId == targetId &&
                    t.CenterId == centerId &&
                    !t.IsDeleted &&
                    t.User != null &&
                    t.User.CenterId == centerId &&
                    !t.User.IsDeleted &&
                    t.User.RoleName == UserRole.Teacher,
                    cancellationToken);

            if (teacher == null)
            {
                return ResetAccountPasswordResult.Failure(ErrorCodes.ResourceNotFound, "Giáo viên không tồn tại hoặc không thuộc quyền quản lý của bạn.");
            }

            user = teacher.User!;
        }
        else if (string.Equals(targetRole, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            var student = await _dbContext.Students
                .Include(s => s.User)
                .FirstOrDefaultAsync(s =>
                    s.StudentId == targetId &&
                    s.CenterId == centerId &&
                    !s.IsDeleted &&
                    s.User != null &&
                    s.User.CenterId == centerId &&
                    !s.User.IsDeleted &&
                    s.User.RoleName == UserRole.Student,
                    cancellationToken);

            if (student == null)
            {
                return ResetAccountPasswordResult.Failure(ErrorCodes.ResourceNotFound, "Học viên không tồn tại hoặc không thuộc quyền quản lý của bạn.");
            }

            user = student.User!;
        }

        var expectedRole = string.Equals(targetRole, nameof(UserRole.Teacher), StringComparison.Ordinal)
            ? UserRole.Teacher
            : UserRole.Student;

        if (user == null ||
            user.RoleName != expectedRole ||
            user.RoleName == UserRole.CenterManager ||
            user.RoleName == UserRole.PlatformAdmin)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (user.RowVersion != expectedVersion)
        {
            return ResetAccountPasswordResult.Failure(ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng đã bị thay đổi bởi một phiên làm việc khác.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var managerId = _tenantContext.UserId.Value;

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.AuthVersion = checked(user.AuthVersion + 1);
        user.UpdatedAt = now;
        user.UpdatedBy = managerId;
        _dbContext.Entry(user).Property(item => item.RowVersion).OriginalValue = expectedVersion;

        var newRowVersion = checked(expectedVersion + 1);

        // Revoke active refresh tokens
        var refreshTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.CenterId == centerId && rt.UserId == user.UserId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in refreshTokens)
        {
            token.RevokedAt = now;
            token.RevokeReason = "Password reset by CenterManager.";
        }

        // Redacted audit log (zero credentials logged)
        _dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = managerId,
            ActionType = string.Equals(targetRole, nameof(UserRole.Teacher), StringComparison.Ordinal)
                ? "TeacherPasswordReset"
                : "StudentPasswordReset",
            TargetType = "User",
            TargetId = user.UserId.ToString("D"),
            TargetUserId = user.UserId,
            BeforeData = JsonSerializer.Serialize(new { RowVersion = expectedVersion, AuthVersion = user.AuthVersion - 1 }),
            AfterData = JsonSerializer.Serialize(new { RowVersion = newRowVersion, AuthVersion = user.AuthVersion }),
            Reason = request.Reason.Trim(),
            TraceId = traceId ?? string.Empty,
            CreatedAt = now,
            CreatedBy = managerId
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict resetting password for user {UserId}", user.UserId);
            _dbContext.ChangeTracker.Clear();
            return ResetAccountPasswordResult.Failure(ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng đã bị thay đổi bởi một phiên làm việc khác.");
        }

        return ResetAccountPasswordResult.Success(user.UserId.ToString("D"), user.RowVersion.ToString(CultureInfo.InvariantCulture));
    }
}
