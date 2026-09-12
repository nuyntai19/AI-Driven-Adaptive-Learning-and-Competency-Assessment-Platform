using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Platform;

public class PlatformCenterService : IPlatformCenterService
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly AuthorizationBootstrapper _authorizationBootstrapper;
    private readonly TimeProvider _timeProvider;

    public PlatformCenterService(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IPasswordHasher<User> passwordHasher,
        AuthorizationBootstrapper authorizationBootstrapper,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _authorizationBootstrapper = authorizationBootstrapper;
        _timeProvider = timeProvider;
    }

    private bool IsAuthorizedPlatformCaller(out Guid callerUserId)
    {
        callerUserId = Guid.Empty;
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value != AuthorizationBootstrapper.ReservedPlatformCenterId ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            _tenantContext.Role != nameof(UserRole.PlatformAdmin))
        {
            return false;
        }

        callerUserId = _tenantContext.UserId.Value;
        return true;
    }

    public async Task<PlatformResult<PlatformCentersListData>> ListCentersAsync(
        int page,
        int pageSize,
        string? search,
        string? status,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out _))
        {
            return PlatformResult<PlatformCentersListData>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền truy cập.");
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);

        var query = _dbContext.Centers
            .IgnoreQueryFilters()
            .Where(c => c.CenterId != AuthorizationBootstrapper.ReservedPlatformCenterId && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(c => c.CenterCode.Contains(trimmedSearch) ||
                                     c.CenterName.Contains(trimmedSearch) ||
                                     _dbContext.Users.IgnoreQueryFilters().Any(u =>
                                         u.CenterId == c.CenterId &&
                                         u.RoleName == UserRole.CenterManager &&
                                         !u.IsDeleted &&
                                         (u.Username.Contains(trimmedSearch) || u.DisplayName.Contains(trimmedSearch))));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CenterStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(c => c.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return PlatformResult<PlatformCentersListData>.Success(new PlatformCentersListData
            {
                Items = Array.Empty<PlatformCenterListItemDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            });
        }

        var pagedCenters = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.CenterId,
                c.CenterCode,
                c.CenterName,
                c.Status,
                c.Timezone,
                c.CreatedAt,
                c.RowVersion
            })
            .ToListAsync(cancellationToken);

        var managerDict = new Dictionary<Guid, (Guid UserId, string Username, string DisplayName, ulong RowVersion)>();
        if (pagedCenters.Count > 0)
        {
            var parameter = Expression.Parameter(typeof(User), "u");
            var property = Expression.Property(parameter, nameof(User.CenterId));
            Expression? orBody = null;
            foreach (var c in pagedCenters)
            {
                var equals = Expression.Equal(property, Expression.Constant(c.CenterId));
                orBody = orBody == null ? equals : Expression.OrElse(orBody, equals);
            }
            var centerFilter = Expression.Lambda<Func<User, bool>>(orBody!, parameter);

            var initialManagers = await _dbContext.Users
                .IgnoreQueryFilters()
                .Where(centerFilter)
                .Where(u => u.RoleName == UserRole.CenterManager && !u.IsDeleted)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new
                {
                    u.CenterId,
                    u.UserId,
                    u.Username,
                    u.DisplayName,
                    u.RowVersion
                })
                .ToListAsync(cancellationToken);

            foreach (var m in initialManagers)
            {
                managerDict.TryAdd(m.CenterId, (m.UserId, m.Username, m.DisplayName, m.RowVersion));
            }
        }

        var items = pagedCenters.Select(c =>
        {
            var hasManager = managerDict.TryGetValue(c.CenterId, out var manager);
            return new PlatformCenterListItemDto
            {
                CenterId = c.CenterId,
                CenterCode = c.CenterCode,
                CenterName = c.CenterName,
                Status = c.Status.ToString(),
                Timezone = c.Timezone,
                CreatedAt = c.CreatedAt,
                RowVersion = c.RowVersion.ToString(CultureInfo.InvariantCulture),
                InitialManagerUserId = hasManager ? manager.UserId : null,
                InitialManagerUsername = hasManager ? manager.Username : null,
                InitialManagerDisplayName = hasManager ? manager.DisplayName : null,
                InitialManagerUserRowVersion = hasManager ? manager.RowVersion.ToString(CultureInfo.InvariantCulture) : null
            };
        }).ToList();

        return PlatformResult<PlatformCentersListData>.Success(new PlatformCentersListData
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<PlatformResult<PlatformCenterListItemDto>> CreateCenterAsync(
        CreatePlatformCenterRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out var callerUserId))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền tạo trung tâm.");
        }

        var centerCode = request.CenterCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var centerName = request.CenterName?.Trim() ?? string.Empty;
        var timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Ho_Chi_Minh" : request.Timezone.Trim();
        var managerUsername = request.InitialManagerUsername?.Trim() ?? string.Empty;
        var managerDisplayName = request.InitialManagerDisplayName?.Trim() ?? string.Empty;
        var managerPassword = request.InitialManagerPassword ?? string.Empty;

        if (string.IsNullOrWhiteSpace(centerCode) || centerCode.Length > 32 ||
            !Regex.IsMatch(centerCode, "^[A-Z0-9_-]+$") ||
            string.IsNullOrWhiteSpace(centerName) || centerName.Length > 200 ||
            string.IsNullOrWhiteSpace(managerUsername) || managerUsername.Length > 100 ||
            string.IsNullOrWhiteSpace(managerDisplayName) || managerDisplayName.Length > 200 ||
            string.IsNullOrWhiteSpace(managerPassword) || managerPassword.Length < 12)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Dữ liệu yêu cầu tạo trung tâm không hợp lệ (mật khẩu phải tối thiểu 12 ký tự).");
        }

        if (centerCode == "PLATFORM")
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ForbiddenResource, "Không được phép tạo trung tâm với mã PLATFORM.");
        }

        var codeExists = await _dbContext.Centers
            .IgnoreQueryFilters()
            .AnyAsync(c => c.CenterCode == centerCode, cancellationToken);
        if (codeExists)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.DuplicateResource, $"Mã trung tâm '{centerCode}' đã tồn tại.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var centerId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();

        var center = new Center
        {
            CenterId = centerId,
            CenterCode = centerCode,
            CenterName = centerName,
            Status = CenterStatus.Active,
            Timezone = timezone,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            RowVersion = 1
        };

        var managerUser = new User
        {
            UserId = managerUserId,
            CenterId = centerId,
            Username = managerUsername,
            DisplayName = managerDisplayName,
            RoleName = UserRole.CenterManager,
            Status = UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = now,
            CreatedBy = callerUserId,
            UpdatedAt = now,
            UpdatedBy = callerUserId,
            IsDeleted = false,
            RowVersion = 1
        };

        managerUser.PasswordHash = _passwordHasher.HashPassword(managerUser, managerPassword);

        var auditLog = new AuthorizationAuditLog
        {
            CenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            ActorUserId = callerUserId,
            TargetUserId = null, // Invariant: Cross-tenant operations MUST record target_user_id as null
            TargetType = "Center",
            TargetId = centerId.ToString("D"),
            ActionType = "CenterCreated",
            BeforeData = null,
            AfterData = JsonSerializer.Serialize(new
            {
                CenterId = centerId,
                CenterCode = centerCode,
                CenterName = centerName,
                Status = center.Status.ToString(),
                InitialManagerUserId = managerUserId,
                InitialManagerUsername = managerUsername
            }),
            Reason = "Platform center provisioned.",
            TraceId = string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId,
            CreatedAt = now,
            CreatedBy = callerUserId
        };

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(managerUser);
        _dbContext.AuthorizationAuditLogs.Add(auditLog);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx) when (dbEx.InnerException?.Message.Contains("ux_centers_center_code") == true ||
                                             dbEx.InnerException?.Message.Contains("1062") == true)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.DuplicateResource, $"Mã trung tâm '{centerCode}' đã tồn tại.");
        }

        await _authorizationBootstrapper.EnsureCenterAsync(centerId, cancellationToken: cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return PlatformResult<PlatformCenterListItemDto>.Success(new PlatformCenterListItemDto
        {
            CenterId = center.CenterId,
            CenterCode = center.CenterCode,
            CenterName = center.CenterName,
            Status = center.Status.ToString(),
            Timezone = center.Timezone,
            CreatedAt = center.CreatedAt,
            RowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture),
            InitialManagerUserId = managerUser.UserId,
            InitialManagerUsername = managerUser.Username,
            InitialManagerDisplayName = managerUser.DisplayName,
            InitialManagerUserRowVersion = managerUser.RowVersion.ToString(CultureInfo.InvariantCulture)
        });
    }

    public async Task<PlatformResult<PlatformCenterListItemDto>> UpdateCenterStatusAsync(
        Guid centerId,
        UpdatePlatformCenterStatusRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out var callerUserId))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền cập nhật trạng thái trung tâm.");
        }

        if (centerId == AuthorizationBootstrapper.ReservedPlatformCenterId)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ForbiddenResource, "Không được phép thay đổi trạng thái của trung tâm PLATFORM.");
        }

        if (!ulong.TryParse(request.EffectiveRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion) ||
            expectedVersion == 0 ||
            string.IsNullOrWhiteSpace(request.Status) ||
            !Enum.TryParse<CenterStatus>(request.Status, true, out var newStatus))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Dữ liệu cập nhật trạng thái trung tâm không hợp lệ.");
        }

        var center = await _dbContext.Centers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && !c.IsDeleted, cancellationToken);

        if (center is null)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ResourceNotFound, $"Không tìm thấy trung tâm với ID '{centerId}'.");
        }

        if (center.RowVersion != expectedVersion)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ConcurrencyConflict, "Dữ liệu trung tâm đã bị thay đổi bởi thao tác khác.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var oldStatus = center.Status.ToString();
        center.Status = newStatus;
        center.UpdatedAt = now;
        center.RowVersion++;

        if (newStatus == CenterStatus.Suspended && oldStatus != CenterStatus.Suspended.ToString())
        {
            var centerUsers = await _dbContext.Users
                .IgnoreQueryFilters()
                .Where(u => u.CenterId == centerId && !u.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var u in centerUsers)
            {
                u.AuthVersion++;
                u.UpdatedAt = now;
                u.UpdatedBy = callerUserId;
            }

            var activeTokens = await _dbContext.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.CenterId == centerId && rt.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var rt in activeTokens)
            {
                rt.RevokedAt = now;
                rt.RevokeReason = "Center suspended by platform administrator.";
            }
        }

        var auditLog = new AuthorizationAuditLog
        {
            CenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            ActorUserId = callerUserId,
            TargetUserId = null, // Invariant: Cross-tenant operations MUST record target_user_id as null
            TargetType = "Center",
            TargetId = center.CenterId.ToString("D"),
            ActionType = "CenterStatusUpdated",
            BeforeData = JsonSerializer.Serialize(new { Status = oldStatus }),
            AfterData = JsonSerializer.Serialize(new { Status = newStatus.ToString() }),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Platform center status updated." : request.Reason.Trim(),
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
        }
        catch (DbUpdateConcurrencyException)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ConcurrencyConflict, "Dữ liệu trung tâm đã bị thay đổi bởi thao tác khác.");
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var manager = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId == centerId && u.RoleName == UserRole.CenterManager && !u.IsDeleted)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.DisplayName,
                u.RowVersion
            })
            .FirstOrDefaultAsync(cancellationToken);

        return PlatformResult<PlatformCenterListItemDto>.Success(new PlatformCenterListItemDto
        {
            CenterId = center.CenterId,
            CenterCode = center.CenterCode,
            CenterName = center.CenterName,
            Status = center.Status.ToString(),
            Timezone = center.Timezone,
            CreatedAt = center.CreatedAt,
            RowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture),
            InitialManagerUserId = manager?.UserId,
            InitialManagerUsername = manager?.Username,
            InitialManagerDisplayName = manager?.DisplayName,
            InitialManagerUserRowVersion = manager?.RowVersion.ToString(CultureInfo.InvariantCulture)
        });
    }

    public async Task<PlatformResult<ResetCenterManagerPasswordData>> ResetCenterManagerPasswordAsync(
        Guid centerId,
        Guid managerUserId,
        ResetCenterManagerPasswordRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out var callerUserId))
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền đặt lại mật khẩu quản lý trung tâm.");
        }

        if (centerId == AuthorizationBootstrapper.ReservedPlatformCenterId)
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ForbiddenResource, "Không được phép đặt lại mật khẩu tài khoản quản trị PLATFORM qua endpoint này.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12 ||
            !ulong.TryParse(request.ExpectedUserRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion) ||
            expectedVersion == 0)
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ValidationFailed, "Dữ liệu đặt lại mật khẩu quản lý không hợp lệ (mật khẩu phải tối thiểu 12 ký tự).");
        }

        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.CenterId == centerId && u.UserId == managerUserId && !u.IsDeleted, cancellationToken);

        if (user is null)
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ResourceNotFound, $"Không tìm thấy người dùng với ID '{managerUserId}' thuộc trung tâm '{centerId}'.");
        }

        if (user.RoleName != UserRole.CenterManager)
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ValidationFailed, "Người dùng được chọn không phải là Quản lý trung tâm (CenterManager).");
        }

        if (user.RowVersion != expectedVersion)
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng đã bị thay đổi bởi thao tác khác.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.RowVersion++;
        user.AuthVersion++; // Invalidate active JWT sessions
        user.UpdatedAt = now;
        user.UpdatedBy = callerUserId;

        // Bulk revoke active refresh tokens for the user
        var refreshTokens = await _dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(rt => rt.CenterId == centerId && rt.UserId == managerUserId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in refreshTokens)
        {
            token.RevokedAt = now;
            token.RevokeReason = "Password reset by platform administrator.";
        }

        // Platform Audit Invariant: CenterId = PLATFORM, target_user_id = null, zero credentials logged
        var auditLog = new AuthorizationAuditLog
        {
            CenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            ActorUserId = callerUserId,
            TargetUserId = null, // Invariant: Cross-tenant operations MUST record target_user_id as null
            TargetType = "CenterManager",
            TargetId = $"{centerId:D}:{managerUserId:D}",
            ActionType = "CenterManagerPasswordReset",
            BeforeData = null, // Zero passwords / hashes logged!
            AfterData = JsonSerializer.Serialize(new
            {
                CenterId = centerId,
                ManagerUserId = managerUserId,
                AuthVersion = user.AuthVersion
            }),
            Reason = "Platform administrator reset manager password.",
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
        }
        catch (DbUpdateConcurrencyException)
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng đã bị thay đổi bởi thao tác khác.");
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return PlatformResult<ResetCenterManagerPasswordData>.Success(new ResetCenterManagerPasswordData
        {
            CenterId = centerId,
            ManagerUserId = managerUserId,
            NewUserRowVersion = user.RowVersion.ToString(CultureInfo.InvariantCulture),
            ResetAtUtc = now,
            Success = true
        });
    }
}
