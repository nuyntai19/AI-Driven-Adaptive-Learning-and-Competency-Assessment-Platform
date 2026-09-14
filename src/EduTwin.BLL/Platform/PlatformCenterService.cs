using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    private void RecordPlatformAudit(
        Guid targetCenterId,
        Guid actorUserId,
        string targetType,
        string targetId,
        string actionType,
        object? beforeData,
        object? afterData,
        string sanitizedReason,
        string traceId,
        DateTime now)
    {
        var auditLog = new AuthorizationAuditLog
        {
            CenterId = AuthorizationBootstrapper.ReservedPlatformCenterId,
            TargetCenterId = targetCenterId,
            ActorUserId = actorUserId,
            TargetUserId = null, // Invariant: Cross-tenant operations MUST record target_user_id as null
            TargetType = targetType,
            TargetId = targetId,
            ActionType = actionType,
            BeforeData = beforeData is null ? null : JsonSerializer.Serialize(beforeData),
            AfterData = afterData is null ? null : JsonSerializer.Serialize(afterData),
            Reason = sanitizedReason,
            TraceId = string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId,
            CreatedAt = now,
            CreatedBy = actorUserId
        };
        _dbContext.AuthorizationAuditLogs.Add(auditLog);
    }

    private async Task<PlatformResult<T>> ExecuteInCenterLockAsync<T>(
        Guid centerId,
        Func<Center, IDbContextTransaction?, CancellationToken, Task<PlatformResult<T>>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = _dbContext.Database.CurrentTransaction is null && _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;

        if (_dbContext.Database.IsRelational())
        {
            // Materialize SELECT ... FOR UPDATE query across the wire to acquire pessimistic row lock
            await _dbContext.Database.SqlQueryRaw<Guid>(
                "SELECT center_id FROM centers WHERE center_id = {0} FOR UPDATE;", centerId)
                .ToListAsync(cancellationToken);
        }

        // Reload center within transaction after acquiring lock
        var center = await _dbContext.Centers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && !c.IsDeleted, cancellationToken);

        if (center is null)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return PlatformResult<T>.Failure(
                ErrorCodes.ResourceNotFound, $"Không tìm thấy trung tâm với ID '{centerId}'.");
        }

        try
        {
            var result = await operation(center, transaction, cancellationToken);
            if (!result.IsSuccess)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                return result;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return PlatformResult<T>.Failure(
                ErrorCodes.ConcurrencyConflict, "Dữ liệu trung tâm đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
        }
        catch (Exception)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
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
                c.RowVersion,
                c.PrimaryManagerUserId
            })
            .ToListAsync(cancellationToken);

        var managerDict = new Dictionary<Guid, (Guid UserId, string Username, string DisplayName, ulong RowVersion)>();
        Dictionary<Guid, int> studentCounts;
        Dictionary<Guid, int> teacherCounts;
        Dictionary<Guid, int> activeManagerCounts;
        Dictionary<Guid, int> classCounts;
        HashSet<Guid> activePrimaryManagerUserIds;

        if (pagedCenters.Count > 0)
        {
            var targetCenterIds = pagedCenters.Select(c => c.CenterId).Distinct().ToList();
            var centerFilter = BuildOrEqualityFilter<User, Guid>(nameof(User.CenterId), targetCenterIds);

            var managers = await _dbContext.Users
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

            // Group by CenterId
            var groupedManagers = managers.GroupBy(m => m.CenterId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var c in pagedCenters)
            {
                if (!groupedManagers.TryGetValue(c.CenterId, out var centerManagers) || centerManagers.Count == 0)
                {
                    continue;
                }

                var primary = c.PrimaryManagerUserId.HasValue
                    ? centerManagers.FirstOrDefault(m => m.UserId == c.PrimaryManagerUserId.Value)
                    : null;

                var chosen = primary ?? centerManagers[0];
                managerDict[c.CenterId] = (chosen.UserId, chosen.Username, chosen.DisplayName, chosen.RowVersion);
            }

            var classCenterFilter = BuildOrEqualityFilter<Class, Guid>(nameof(Class.CenterId), targetCenterIds);

            studentCounts = await _dbContext.Users
                .IgnoreQueryFilters()
                .Where(centerFilter)
                .Where(u => u.RoleName == UserRole.Student && u.Status == UserStatus.Active && !u.IsDeleted)
                .GroupBy(u => u.CenterId)
                .Select(g => new { CenterId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CenterId, x => x.Count, cancellationToken);

            teacherCounts = await _dbContext.Users
                .IgnoreQueryFilters()
                .Where(centerFilter)
                .Where(u => u.RoleName == UserRole.Teacher && u.Status == UserStatus.Active && !u.IsDeleted)
                .GroupBy(u => u.CenterId)
                .Select(g => new { CenterId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CenterId, x => x.Count, cancellationToken);

            activeManagerCounts = await _dbContext.Users
                .IgnoreQueryFilters()
                .Where(centerFilter)
                .Where(u => u.RoleName == UserRole.CenterManager && u.Status == UserStatus.Active && !u.IsDeleted)
                .GroupBy(u => u.CenterId)
                .Select(g => new { CenterId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CenterId, x => x.Count, cancellationToken);

            classCounts = await _dbContext.Classes
                .IgnoreQueryFilters()
                .Where(classCenterFilter)
                .Where(cl => !cl.IsDeleted)
                .GroupBy(cl => cl.CenterId)
                .Select(g => new { CenterId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CenterId, x => x.Count, cancellationToken);

            var primaryManagerIds = pagedCenters
                .Where(c => c.PrimaryManagerUserId.HasValue)
                .Select(c => c.PrimaryManagerUserId!.Value)
                .Distinct()
                .ToList();

            if (primaryManagerIds.Count > 0)
            {
                var pmFilter = BuildOrEqualityFilter<User, Guid>(nameof(User.UserId), primaryManagerIds);
                activePrimaryManagerUserIds = (await _dbContext.Users
                    .IgnoreQueryFilters()
                    .Where(pmFilter)
                    .Where(u => u.RoleName == UserRole.CenterManager && u.Status == UserStatus.Active && !u.IsDeleted)
                    .Select(u => u.UserId)
                    .ToListAsync(cancellationToken))
                    .ToHashSet();
            }
            else
            {
                activePrimaryManagerUserIds = new HashSet<Guid>();
            }
        }
        else
        {
            studentCounts = new Dictionary<Guid, int>();
            teacherCounts = new Dictionary<Guid, int>();
            activeManagerCounts = new Dictionary<Guid, int>();
            classCounts = new Dictionary<Guid, int>();
            activePrimaryManagerUserIds = new HashSet<Guid>();
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
                PrimaryManagerUserId = hasManager ? manager.UserId : null,
                PrimaryManagerUsername = hasManager ? manager.Username : null,
                PrimaryManagerDisplayName = hasManager ? manager.DisplayName : null,
                PrimaryManagerUserRowVersion = hasManager ? manager.RowVersion.ToString(CultureInfo.InvariantCulture) : null,
                InitialManagerUserId = hasManager ? manager.UserId : null,
                InitialManagerUsername = hasManager ? manager.Username : null,
                InitialManagerDisplayName = hasManager ? manager.DisplayName : null,
                InitialManagerUserRowVersion = hasManager ? manager.RowVersion.ToString(CultureInfo.InvariantCulture) : null,
                ActiveStudentCount = studentCounts.GetValueOrDefault(c.CenterId, 0),
                ActiveTeacherCount = teacherCounts.GetValueOrDefault(c.CenterId, 0),
                ClassCount = classCounts.GetValueOrDefault(c.CenterId, 0),
                ActiveManagerCount = activeManagerCounts.GetValueOrDefault(c.CenterId, 0),
                HasActivePrimaryManager = c.PrimaryManagerUserId.HasValue && activePrimaryManagerUserIds.Contains(c.PrimaryManagerUserId.Value)
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

        var centerCode = NormalizeAscii(request.CenterCode, toUpper: true).Replace(" ", "-");
        var centerName = request.CenterName?.Trim() ?? string.Empty;
        var rawTimezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Ho_Chi_Minh" : request.Timezone.Trim();
        var managerUsername = NormalizeAscii(request.InitialManagerUsername).Replace(" ", "_");
        var managerDisplayName = request.InitialManagerDisplayName?.Trim() ?? string.Empty;
        var managerPassword = request.InitialManagerPassword ?? string.Empty;

        if (!PlatformTimezoneValidator.IsValid(rawTimezone, out var timezone))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Múi giờ không hợp lệ. Vui lòng chọn múi giờ chuẩn IANA (ví dụ: Asia/Ho_Chi_Minh, UTC).");
        }

        if (string.IsNullOrWhiteSpace(centerCode) || centerCode.Length > 32 || !Regex.IsMatch(centerCode, "^[A-Z0-9_-]+$"))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Mã trung tâm không hợp lệ (chỉ gồm chữ in hoa không dấu A-Z, chữ số 0-9, dấu '-' hoặc '_', không chứa khoảng trắng, tối đa 32 ký tự).");
        }

        if (string.IsNullOrWhiteSpace(centerName) || centerName.Length > 200)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Tên trung tâm không được để trống và không quá 200 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(managerUsername) || managerUsername.Length > 100 || !Regex.IsMatch(managerUsername, "^[a-zA-Z0-9._-]+$"))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Tên đăng nhập quản lý không hợp lệ (chỉ gồm chữ cái không dấu, chữ số, dấu '.', '-', '_', không chứa khoảng trắng hoặc ký tự có dấu, tối đa 100 ký tự).");
        }

        if (string.IsNullOrWhiteSpace(managerDisplayName) || managerDisplayName.Length > 200)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Họ và tên hiển thị quản lý không được để trống và không quá 200 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(managerPassword) || managerPassword.Length < 12)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Mật khẩu quản lý ban đầu phải có tối thiểu 12 ký tự.");
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
            PrimaryManagerUserId = null, // Set null initially to prevent cyclic dependency during initial insert
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

        RecordPlatformAudit(
            centerId,
            callerUserId,
            "Center",
            centerId.ToString("D"),
            "CenterCreated",
            null,
            new
            {
                CenterId = centerId,
                CenterCode = centerCode,
                CenterName = centerName,
                Status = center.Status.ToString(),
                InitialManagerUserId = managerUserId,
                InitialManagerUsername = managerUsername,
                PrimaryManagerUserId = managerUserId
            },
            "Platform center provisioned.",
            traceId,
            now);

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        _dbContext.Centers.Add(center);
        _dbContext.Users.Add(managerUser);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            center.PrimaryManagerUserId = managerUserId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException dbEx)
        {
            var mysql = (dbEx.GetBaseException() as MySql.Data.MySqlClient.MySqlException)
                        ?? (dbEx.InnerException as MySql.Data.MySqlClient.MySqlException);

            if (mysql?.Number == 1062 &&
                mysql.Message.Contains("ux_centers_center_code", StringComparison.OrdinalIgnoreCase))
            {
                return PlatformResult<PlatformCenterListItemDto>.Failure(
                    ErrorCodes.DuplicateResource, $"Mã trung tâm '{centerCode}' đã tồn tại.");
            }

            var message = dbEx.GetBaseException()?.Message ?? dbEx.InnerException?.Message ?? string.Empty;
            if (message.Contains("1062", StringComparison.OrdinalIgnoreCase) &&
                message.Contains("ux_centers_center_code", StringComparison.OrdinalIgnoreCase))
            {
                return PlatformResult<PlatformCenterListItemDto>.Failure(
                    ErrorCodes.DuplicateResource, $"Mã trung tâm '{centerCode}' đã tồn tại.");
            }

            throw;
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
            PrimaryManagerUserId = managerUser.UserId,
            PrimaryManagerUsername = managerUser.Username,
            PrimaryManagerDisplayName = managerUser.DisplayName,
            PrimaryManagerUserRowVersion = managerUser.RowVersion.ToString(CultureInfo.InvariantCulture),
            InitialManagerUserId = managerUser.UserId,
            InitialManagerUsername = managerUser.Username,
            InitialManagerDisplayName = managerUser.DisplayName,
            InitialManagerUserRowVersion = managerUser.RowVersion.ToString(CultureInfo.InvariantCulture),
            ActiveStudentCount = 0,
            ActiveTeacherCount = 0,
            ClassCount = 0,
            ActiveManagerCount = 1,
            HasActivePrimaryManager = true
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

        return await ExecuteInCenterLockAsync(centerId, async (center, transaction, ct) =>
        {
            if (center.RowVersion != expectedVersion)
            {
                return PlatformResult<PlatformCenterListItemDto>.Failure(
                    ErrorCodes.ConcurrencyConflict, "Dữ liệu trung tâm đã bị thay đổi bởi thao tác khác.");
            }

            var oldStatus = center.Status.ToString();
            if (newStatus == center.Status)
            {
                return PlatformResult<PlatformCenterListItemDto>.Failure(
                    ErrorCodes.ValidationFailed, $"Trung tâm đã ở trạng thái {newStatus}.");
            }

            string sanitizedReason = "Platform center status updated.";
            if (newStatus != center.Status)
            {
                if (string.IsNullOrWhiteSpace(request.Reason) || !PlatformAuditSanitizer.ValidateAndSanitizeReason(request.Reason, out sanitizedReason, out var reasonError, minLength: 5))
                {
                    return PlatformResult<PlatformCenterListItemDto>.Failure(
                        ErrorCodes.ValidationFailed, "Lý do thay đổi trạng thái trung tâm là bắt buộc (từ 5 đến 500 ký tự).");
                }
            }

            if (newStatus == CenterStatus.Active)
            {
                if (!center.PrimaryManagerUserId.HasValue)
                {
                    return PlatformResult<PlatformCenterListItemDto>.Failure(
                        ErrorCodes.ValidationFailed, "Trung tâm không thể kích hoạt do chưa có Quản lý chính (Primary Manager).");
                }

                var primaryManager = await _dbContext.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.CenterId == centerId && u.UserId == center.PrimaryManagerUserId.Value && !u.IsDeleted, ct);

                if (primaryManager is null || primaryManager.RoleName != UserRole.CenterManager || primaryManager.Status != UserStatus.Active)
                {
                    return PlatformResult<PlatformCenterListItemDto>.Failure(
                        ErrorCodes.ValidationFailed, "Trung tâm không thể kích hoạt do Quản lý chính không ở trạng thái hoạt động (Active).");
                }
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            center.Status = newStatus;
            center.UpdatedAt = now;
            center.RowVersion++;

            var affectedUserCount = 0;
            var revokedTokenCount = 0;

            if (newStatus == CenterStatus.Suspended && oldStatus != CenterStatus.Suspended.ToString())
            {
                var centerUsers = await _dbContext.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.CenterId == centerId && !u.IsDeleted)
                    .ToListAsync(ct);
                affectedUserCount = centerUsers.Count;
                foreach (var u in centerUsers)
                {
                    u.AuthVersion++;
                    u.UpdatedAt = now;
                    u.UpdatedBy = callerUserId;
                }

                var activeTokens = await _dbContext.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(rt => rt.CenterId == centerId && rt.RevokedAt == null)
                    .ToListAsync(ct);
                revokedTokenCount = activeTokens.Count;
                foreach (var rt in activeTokens)
                {
                    rt.RevokedAt = now;
                    rt.RevokeReason = "Center suspended by platform administrator.";
                }
            }

            object afterDataPayload = newStatus == CenterStatus.Suspended
                ? new { Status = newStatus.ToString(), AffectedUserCount = affectedUserCount, RevokedTokenCount = revokedTokenCount }
                : new { Status = newStatus.ToString(), Reactivated = true };

            RecordPlatformAudit(
                center.CenterId,
                callerUserId,
                "Center",
                center.CenterId.ToString("D"),
                "CenterStatusUpdated",
                new { Status = oldStatus },
                afterDataPayload,
                sanitizedReason,
                traceId,
                now);

            // Canonical query: Find current primary manager by center.PrimaryManagerUserId
            var primaryId = center.PrimaryManagerUserId;
            var manager = primaryId.HasValue
                ? await _dbContext.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.CenterId == centerId && u.UserId == primaryId.Value && !u.IsDeleted)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.DisplayName,
                        u.RowVersion
                    })
                    .FirstOrDefaultAsync(ct)
                : null;

            var aggregates = await GetSafeAggregatesForCenterAsync(center.CenterId, center.PrimaryManagerUserId, ct);

            return PlatformResult<PlatformCenterListItemDto>.Success(new PlatformCenterListItemDto
            {
                CenterId = center.CenterId,
                CenterCode = center.CenterCode,
                CenterName = center.CenterName,
                Status = center.Status.ToString(),
                Timezone = center.Timezone,
                CreatedAt = center.CreatedAt,
                RowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture),
                PrimaryManagerUserId = manager?.UserId,
                PrimaryManagerUsername = manager?.Username,
                PrimaryManagerDisplayName = manager?.DisplayName,
                PrimaryManagerUserRowVersion = manager?.RowVersion.ToString(CultureInfo.InvariantCulture),
                InitialManagerUserId = manager?.UserId,
                InitialManagerUsername = manager?.Username,
                InitialManagerDisplayName = manager?.DisplayName,
                InitialManagerUserRowVersion = manager?.RowVersion.ToString(CultureInfo.InvariantCulture),
                ActiveStudentCount = aggregates.StudentCount,
                ActiveTeacherCount = aggregates.TeacherCount,
                ClassCount = aggregates.ClassCount,
                ActiveManagerCount = aggregates.ManagerCount,
                HasActivePrimaryManager = aggregates.HasActivePrimary
            });
        }, cancellationToken);
    }

    public async Task<PlatformResult<PlatformCenterListItemDto>> UpdateCenterMetadataAsync(
        Guid centerId,
        UpdateCenterMetadataRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out var callerUserId))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền cập nhật trung tâm.");
        }

        if (centerId == AuthorizationBootstrapper.ReservedPlatformCenterId)
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ForbiddenResource, "Không được sửa đổi Root Tenant PLATFORM.");
        }

        if (!PlatformAuditSanitizer.ValidateAndSanitizeReason(request.Reason, out var sanitizedReason, out var reasonError))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, reasonError ?? "Lý do cập nhật thông tin trung tâm không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedRowVersion) ||
            !ulong.TryParse(request.ExpectedRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion))
        {
            return PlatformResult<PlatformCenterListItemDto>.Failure(
                ErrorCodes.ValidationFailed, "Mã phiên bản (expectedRowVersion) không hợp lệ.");
        }

        string? normalizedTimezone = null;
        if (!string.IsNullOrWhiteSpace(request.Timezone))
        {
            if (!PlatformTimezoneValidator.IsValid(request.Timezone, out normalizedTimezone))
            {
                return PlatformResult<PlatformCenterListItemDto>.Failure(
                    ErrorCodes.ValidationFailed, "Múi giờ không hợp lệ. Vui lòng chọn múi giờ chuẩn IANA (ví dụ: Asia/Ho_Chi_Minh, UTC).");
            }
        }

        return await ExecuteInCenterLockAsync(centerId, async (center, transaction, ct) =>
        {
            if (center.RowVersion != expectedVersion)
            {
                return PlatformResult<PlatformCenterListItemDto>.Failure(
                    ErrorCodes.ConcurrencyConflict,
                    $"Dữ liệu trung tâm đã bị thay đổi bởi phiên làm việc khác (phiên bản hiện tại: {center.RowVersion}, phiên bản yêu cầu: {expectedVersion}).");
            }

            var oldName = center.CenterName;
            var oldTimezone = center.Timezone;
            var oldRowVersion = center.RowVersion;

            if (!string.IsNullOrWhiteSpace(request.CenterName))
            {
                var newName = request.CenterName.Trim();
                if (newName.Length < 3 || newName.Length > 200)
                {
                    return PlatformResult<PlatformCenterListItemDto>.Failure(
                        ErrorCodes.ValidationFailed, "Tên trung tâm phải có độ dài từ 3 đến 200 ký tự.");
                }
                center.CenterName = newName;
            }

            if (!string.IsNullOrWhiteSpace(normalizedTimezone))
            {
                center.Timezone = normalizedTimezone;
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            center.RowVersion++;
            center.UpdatedAt = now;

            RecordPlatformAudit(
                center.CenterId,
                callerUserId,
                "Center",
                center.CenterId.ToString("D"),
                "CenterMetadataUpdated",
                new
                {
                    CenterName = oldName,
                    Timezone = oldTimezone,
                    RowVersion = oldRowVersion
                },
                new
                {
                    CenterName = center.CenterName,
                    Timezone = center.Timezone,
                    RowVersion = center.RowVersion
                },
                sanitizedReason,
                traceId,
                now);

            var manager = center.PrimaryManagerUserId.HasValue
                ? await _dbContext.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.CenterId == center.CenterId && u.UserId == center.PrimaryManagerUserId.Value && !u.IsDeleted)
                    .Select(u => new
                    {
                        u.UserId,
                        u.Username,
                        u.DisplayName,
                        u.RowVersion
                    })
                    .FirstOrDefaultAsync(ct)
                : null;

            var aggregates = await GetSafeAggregatesForCenterAsync(center.CenterId, center.PrimaryManagerUserId, ct);

            return PlatformResult<PlatformCenterListItemDto>.Success(new PlatformCenterListItemDto
            {
                CenterId = center.CenterId,
                CenterCode = center.CenterCode,
                CenterName = center.CenterName,
                Status = center.Status.ToString(),
                Timezone = center.Timezone,
                CreatedAt = center.CreatedAt,
                RowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture),
                PrimaryManagerUserId = manager?.UserId,
                PrimaryManagerUsername = manager?.Username,
                PrimaryManagerDisplayName = manager?.DisplayName,
                PrimaryManagerUserRowVersion = manager?.RowVersion.ToString(CultureInfo.InvariantCulture),
                InitialManagerUserId = manager?.UserId,
                InitialManagerUsername = manager?.Username,
                InitialManagerDisplayName = manager?.DisplayName,
                InitialManagerUserRowVersion = manager?.RowVersion.ToString(CultureInfo.InvariantCulture),
                ActiveStudentCount = aggregates.StudentCount,
                ActiveTeacherCount = aggregates.TeacherCount,
                ClassCount = aggregates.ClassCount,
                ActiveManagerCount = aggregates.ManagerCount,
                HasActivePrimaryManager = aggregates.HasActivePrimary
            });
        }, cancellationToken);
    }

    private async Task<(int StudentCount, int TeacherCount, int ClassCount, int ManagerCount, bool HasActivePrimary)> GetSafeAggregatesForCenterAsync(
        Guid centerId,
        Guid? primaryManagerUserId,
        CancellationToken cancellationToken)
    {
        var studentCount = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId == centerId && u.RoleName == UserRole.Student && u.Status == UserStatus.Active && !u.IsDeleted)
            .CountAsync(cancellationToken);

        var teacherCount = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId == centerId && u.RoleName == UserRole.Teacher && u.Status == UserStatus.Active && !u.IsDeleted)
            .CountAsync(cancellationToken);

        var classCount = await _dbContext.Classes
            .IgnoreQueryFilters()
            .Where(cl => cl.CenterId == centerId && !cl.IsDeleted)
            .CountAsync(cancellationToken);

        var managerCount = await _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId == centerId && u.RoleName == UserRole.CenterManager && u.Status == UserStatus.Active && !u.IsDeleted)
            .CountAsync(cancellationToken);

        var hasActivePrimary = false;
        if (primaryManagerUserId.HasValue)
        {
            hasActivePrimary = await _dbContext.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.UserId == primaryManagerUserId.Value && u.RoleName == UserRole.CenterManager && u.Status == UserStatus.Active && !u.IsDeleted, cancellationToken);
        }

        return (studentCount, teacherCount, classCount, managerCount, hasActivePrimary);
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

        string? reasonError = null;
        if (string.IsNullOrWhiteSpace(request.Reason) ||
            !PlatformAuditSanitizer.ValidateAndSanitizeReason(request.Reason, out var sanitizedReason, out reasonError, minLength: 5))
        {
            return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                ErrorCodes.ValidationFailed, reasonError ?? "Lý do đặt lại mật khẩu là bắt buộc (từ 5 đến 500 ký tự).");
        }

        return await ExecuteInCenterLockAsync(centerId, async (center, transaction, ct) =>
        {
            var user = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.CenterId == centerId && u.UserId == managerUserId && !u.IsDeleted, ct);

            // Fail-closed non-manager: Return ResourceNotFound (404) if user is not found OR not a CenterManager
            // to prevent probing / account enumeration of students or teachers
            if (user is null || user.RoleName != UserRole.CenterManager)
            {
                return PlatformResult<ResetCenterManagerPasswordData>.Failure(
                    ErrorCodes.ResourceNotFound, $"Không tìm thấy quản lý trung tâm với ID '{managerUserId}' thuộc trung tâm '{centerId}'.");
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
                .ToListAsync(ct);

            foreach (var token in refreshTokens)
            {
                token.RevokedAt = now;
                token.RevokeReason = "Password reset by platform administrator.";
            }

            center.RowVersion++;
            center.UpdatedAt = now;

            // Platform Audit Invariant: CenterId = PLATFORM, target_user_id = null, zero credentials logged
            RecordPlatformAudit(
                centerId,
                callerUserId,
                "CenterManager",
                $"{centerId:D}:{managerUserId:D}",
                "CenterManagerPasswordReset",
                null, // Zero passwords / hashes logged!
                new
                {
                    CenterId = centerId,
                    ManagerUserId = managerUserId,
                    AuthVersion = user.AuthVersion
                },
                sanitizedReason,
                traceId,
                now);

            return PlatformResult<ResetCenterManagerPasswordData>.Success(new ResetCenterManagerPasswordData
            {
                CenterId = centerId,
                ManagerUserId = managerUserId,
                NewUserRowVersion = user.RowVersion.ToString(CultureInfo.InvariantCulture),
                ResetAtUtc = now,
                Success = true
            });
        }, cancellationToken);
    }

    public async Task<PlatformResult<PlatformCenterManagersListData>> ListCenterManagersAsync(
        Guid centerId,
        int page,
        int pageSize,
        string? search,
        string? status,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out _))
        {
            return PlatformResult<PlatformCenterManagersListData>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền truy cập.");
        }

        if (centerId == AuthorizationBootstrapper.ReservedPlatformCenterId)
        {
            return PlatformResult<PlatformCenterManagersListData>.Failure(
                ErrorCodes.ForbiddenResource, "Không được phép truy cập quản lý của trung tâm PLATFORM.");
        }

        var center = await _dbContext.Centers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CenterId == centerId && !c.IsDeleted, cancellationToken);

        if (center is null)
        {
            return PlatformResult<PlatformCenterManagersListData>.Failure(
                ErrorCodes.ResourceNotFound, $"Không tìm thấy trung tâm với ID '{centerId}'.");
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);

        var query = _dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId == centerId && u.RoleName == UserRole.CenterManager && !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmedSearch = search.Trim();
            query = query.Where(u => u.Username.Contains(trimmedSearch) || u.DisplayName.Contains(trimmedSearch));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(u => u.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var primaryManagerId = center.PrimaryManagerUserId;

        var managers = await query
            .OrderBy(u => u.UserId == primaryManagerId ? 0 : 1)
            .ThenByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new PlatformCenterManagerListItemDto
            {
                UserId = u.UserId,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Status = u.Status.ToString(),
                IsPrimary = primaryManagerId.HasValue && u.UserId == primaryManagerId.Value,
                CreatedAt = u.CreatedAt,
                RowVersion = u.RowVersion.ToString(CultureInfo.InvariantCulture),
                AuthVersion = u.AuthVersion
            })
            .ToListAsync(cancellationToken);

        return PlatformResult<PlatformCenterManagersListData>.Success(new PlatformCenterManagersListData
        {
            Items = managers,
            TotalCount = totalCount,
            PrimaryManagerUserId = primaryManagerId,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<PlatformResult<CreateCenterManagerResponseData>> CreateCenterManagerAsync(
        Guid centerId,
        CreateCenterManagerRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out var callerUserId))
        {
            return PlatformResult<CreateCenterManagerResponseData>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền tạo quản lý trung tâm.");
        }

        if (centerId == AuthorizationBootstrapper.ReservedPlatformCenterId)
        {
            return PlatformResult<CreateCenterManagerResponseData>.Failure(
                ErrorCodes.ForbiddenResource, "Không được phép tạo quản lý trung tâm trong trung tâm PLATFORM.");
        }

        var username = NormalizeAscii(request.Username).Replace(" ", "_");
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || username.Length > 100 || !Regex.IsMatch(username, "^[a-zA-Z0-9._-]+$"))
        {
            return PlatformResult<CreateCenterManagerResponseData>.Failure(
                ErrorCodes.ValidationFailed, "Tên đăng nhập quản lý không hợp lệ (chỉ gồm chữ cái không dấu, chữ số, dấu '.', '-', '_', tối đa 100 ký tự).");
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
        {
            return PlatformResult<CreateCenterManagerResponseData>.Failure(
                ErrorCodes.ValidationFailed, "Họ và tên hiển thị quản lý không được để trống và không quá 200 ký tự.");
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            return PlatformResult<CreateCenterManagerResponseData>.Failure(
                ErrorCodes.ValidationFailed, "Mật khẩu quản lý phải có tối thiểu 12 ký tự.");
        }

        if (!PlatformAuditSanitizer.ValidateAndSanitizeReason(request.Reason, out var sanitizedReason, out var reasonError))
        {
            return PlatformResult<CreateCenterManagerResponseData>.Failure(
                ErrorCodes.ValidationFailed, reasonError ?? "Lý do tạo quản lý không hợp lệ.");
        }

        if (!ulong.TryParse(request.ExpectedCenterRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedCenterVersion) ||
            expectedCenterVersion == 0)
        {
            return PlatformResult<CreateCenterManagerResponseData>.Failure(
                ErrorCodes.ValidationFailed, "Phiên bản dữ liệu trung tâm (ExpectedCenterRowVersion) không hợp lệ.");
        }

        return await ExecuteInCenterLockAsync(centerId, async (center, transaction, ct) =>
        {
            if (center.RowVersion != expectedCenterVersion)
            {
                return PlatformResult<CreateCenterManagerResponseData>.Failure(
                    ErrorCodes.ConcurrencyConflict, "Dữ liệu trung tâm đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
            }

            var usernameExists = await _dbContext.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.CenterId == centerId && u.Username == username && !u.IsDeleted, ct);

            if (usernameExists)
            {
                return PlatformResult<CreateCenterManagerResponseData>.Failure(
                    ErrorCodes.DuplicateResource, $"Tên người dùng '{username}' đã tồn tại trong trung tâm này.");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var newManagerUserId = Guid.NewGuid();

            var newManager = new User
            {
                UserId = newManagerUserId,
                CenterId = centerId,
                Username = username,
                DisplayName = displayName,
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

            newManager.PasswordHash = _passwordHasher.HashPassword(newManager, password);

            var isPrimary = false;
            if (!center.PrimaryManagerUserId.HasValue)
            {
                center.PrimaryManagerUserId = newManagerUserId;
                isPrimary = true;
            }

            center.RowVersion++;
            center.UpdatedAt = now;

            _dbContext.Users.Add(newManager);

            RecordPlatformAudit(
                centerId,
                callerUserId,
                "User",
                $"{centerId:D}:{newManagerUserId:D}",
                "CenterManagerCreated",
                null,
                new
                {
                    CenterId = centerId,
                    UserId = newManagerUserId,
                    Username = username,
                    DisplayName = displayName,
                    IsPrimary = isPrimary
                },
                sanitizedReason,
                traceId,
                now);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException dbEx)
            {
                var mysql = (dbEx.GetBaseException() as MySql.Data.MySqlClient.MySqlException)
                            ?? (dbEx.InnerException as MySql.Data.MySqlClient.MySqlException);

                if (mysql?.Number == 1062 &&
                    mysql.Message.Contains("ux_users_center_id_username", StringComparison.OrdinalIgnoreCase))
                {
                    return PlatformResult<CreateCenterManagerResponseData>.Failure(
                        ErrorCodes.DuplicateResource, $"Tên người dùng '{username}' đã tồn tại trong trung tâm.");
                }

                var message = dbEx.GetBaseException()?.Message ?? dbEx.InnerException?.Message ?? string.Empty;
                if (message.Contains("1062", StringComparison.OrdinalIgnoreCase) &&
                    message.Contains("ux_users_center_id_username", StringComparison.OrdinalIgnoreCase))
                {
                    return PlatformResult<CreateCenterManagerResponseData>.Failure(
                        ErrorCodes.DuplicateResource, $"Tên người dùng '{username}' đã tồn tại trong trung tâm.");
                }

                throw;
            }

            await _authorizationBootstrapper.EnsureCenterAsync(centerId, cancellationToken: ct);

            return PlatformResult<CreateCenterManagerResponseData>.Success(new CreateCenterManagerResponseData
            {
                UserId = newManager.UserId,
                CenterId = centerId,
                Username = newManager.Username,
                DisplayName = newManager.DisplayName,
                Status = newManager.Status.ToString(),
                IsPrimary = isPrimary,
                CreatedAt = newManager.CreatedAt,
                RowVersion = newManager.RowVersion.ToString(CultureInfo.InvariantCulture),
                CenterRowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture)
            });
        }, cancellationToken);
    }

    public async Task<PlatformResult<UpdateCenterManagerStatusData>> UpdateCenterManagerStatusAsync(
        Guid centerId,
        Guid userId,
        UpdateCenterManagerStatusRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out var callerUserId))
        {
            return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền cập nhật trạng thái quản lý trung tâm.");
        }

        if (centerId == AuthorizationBootstrapper.ReservedPlatformCenterId)
        {
            return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                ErrorCodes.ForbiddenResource, "Không được phép thay đổi trạng thái tài khoản trong trung tâm PLATFORM qua endpoint này.");
        }

        if (string.IsNullOrWhiteSpace(request.Status) ||
            !Enum.TryParse<UserStatus>(request.Status, true, out var newStatus) ||
            !ulong.TryParse(request.ExpectedUserRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedVersion) ||
            expectedVersion == 0)
        {
            return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                ErrorCodes.ValidationFailed, "Dữ liệu cập nhật trạng thái không hợp lệ (bắt buộc trạng thái hợp lệ và row version).");
        }

        if (!PlatformAuditSanitizer.ValidateAndSanitizeReason(request.Reason, out var sanitizedReason, out var reasonError))
        {
            return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                ErrorCodes.ValidationFailed, reasonError ?? "Lý do cập nhật trạng thái không hợp lệ.");
        }

        return await ExecuteInCenterLockAsync(centerId, async (center, transaction, ct) =>
        {
            var user = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.CenterId == centerId && u.UserId == userId && !u.IsDeleted, ct);

            if (user is null || user.RoleName != UserRole.CenterManager)
            {
                return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                    ErrorCodes.ResourceNotFound, "Không tìm thấy quản lý trung tâm hoặc người dùng không thuộc trung tâm này.");
            }

            if (user.RowVersion != expectedVersion)
            {
                return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                    ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
            }

            var oldStatus = user.Status;

            // Invariant 1: Cannot lock/disable primary manager without prior transfer
            if (center.PrimaryManagerUserId == userId && newStatus != UserStatus.Active)
            {
                return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                    ErrorCodes.ValidationFailed, "Không thể khóa hoặc vô hiệu hóa Quản lý chính (Primary Manager). Vui lòng chỉ định Quản lý chính mới trước khi khóa/vô hiệu hóa.");
            }

            // Invariant 2: Cannot lock/disable last active manager of an active center
            if (center.Status == CenterStatus.Active && oldStatus == UserStatus.Active && newStatus != UserStatus.Active)
            {
                var activeCount = await _dbContext.Users
                    .IgnoreQueryFilters()
                    .CountAsync(u => u.CenterId == centerId && u.RoleName == UserRole.CenterManager && u.Status == UserStatus.Active && !u.IsDeleted, ct);

                if (activeCount <= 1)
                {
                    return PlatformResult<UpdateCenterManagerStatusData>.Failure(
                        ErrorCodes.ValidationFailed, "Không thể khóa hoặc vô hiệu hóa quản lý đang hoạt động cuối cùng của trung tâm.");
                }
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            user.Status = newStatus;
            user.RowVersion++;
            user.UpdatedAt = now;
            user.UpdatedBy = callerUserId;

            if (newStatus == UserStatus.Locked || newStatus == UserStatus.Disabled)
            {
                user.AuthVersion++; // Invalidate active JWT sessions

                var refreshTokens = await _dbContext.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(rt => rt.CenterId == centerId && rt.UserId == userId && rt.RevokedAt == null)
                    .ToListAsync(ct);

                foreach (var token in refreshTokens)
                {
                    token.RevokedAt = now;
                    token.RevokeReason = $"Manager status changed to {newStatus} by platform administrator.";
                }
            }

            center.RowVersion++;
            center.UpdatedAt = now;

            RecordPlatformAudit(
                centerId,
                callerUserId,
                "User",
                $"{centerId:D}:{userId:D}",
                $"CenterManagerStatusChanged:{newStatus}",
                new { Status = oldStatus.ToString(), RowVersion = expectedVersion },
                new { Status = newStatus.ToString(), RowVersion = user.RowVersion },
                sanitizedReason,
                traceId,
                now);

            return PlatformResult<UpdateCenterManagerStatusData>.Success(new UpdateCenterManagerStatusData
            {
                UserId = user.UserId,
                CenterId = centerId,
                Status = user.Status.ToString(),
                IsPrimary = center.PrimaryManagerUserId == user.UserId,
                RowVersion = user.RowVersion.ToString(CultureInfo.InvariantCulture),
                UpdatedAtUtc = now
            });
        }, cancellationToken);
    }

    public async Task<PlatformResult<MakePrimaryCenterManagerData>> MakePrimaryCenterManagerAsync(
        Guid centerId,
        Guid userId,
        MakePrimaryCenterManagerRequest request,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedPlatformCaller(out var callerUserId))
        {
            return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                ErrorCodes.ForbiddenResource, "Chỉ quản trị viên nền tảng mới có quyền chỉ định Quản lý chính.");
        }

        if (centerId == AuthorizationBootstrapper.ReservedPlatformCenterId)
        {
            return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                ErrorCodes.ForbiddenResource, "Không được phép chỉ định Quản lý chính cho trung tâm PLATFORM.");
        }

        if (!ulong.TryParse(request.ExpectedCenterRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedCenterVer) ||
            expectedCenterVer == 0 ||
            !ulong.TryParse(request.ExpectedManagerUserRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedManagerVer) ||
            expectedManagerVer == 0)
        {
            return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                ErrorCodes.ValidationFailed, "Dữ liệu chỉ định Quản lý chính không hợp lệ (bắt buộc row versions).");
        }

        if (!PlatformAuditSanitizer.ValidateAndSanitizeReason(request.Reason, out var sanitizedReason, out var reasonError))
        {
            return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                ErrorCodes.ValidationFailed, reasonError ?? "Lý do chỉ định Quản lý chính không hợp lệ.");
        }

        return await ExecuteInCenterLockAsync(centerId, async (center, transaction, ct) =>
        {
            if (center.RowVersion != expectedCenterVer)
            {
                return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                    ErrorCodes.ConcurrencyConflict, "Dữ liệu trung tâm đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
            }

            var targetUser = await _dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.CenterId == centerId && u.UserId == userId && !u.IsDeleted, ct);

            if (targetUser is null || targetUser.RoleName != UserRole.CenterManager)
            {
                return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                    ErrorCodes.ResourceNotFound, "Không tìm thấy quản lý trung tâm hoặc người dùng không thuộc trung tâm này.");
            }

            if (targetUser.RowVersion != expectedManagerVer)
            {
                return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                    ErrorCodes.ConcurrencyConflict, "Dữ liệu người dùng quản lý đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
            }

            if (targetUser.Status != UserStatus.Active)
            {
                return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                    ErrorCodes.ValidationFailed, "Chỉ quản lý đang hoạt động (Active) mới có thể được chỉ định làm Quản lý chính.");
            }

            var oldPrimaryUserId = center.PrimaryManagerUserId;
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            User? prevPrimaryUser = null;
            if (request.DisablePreviousPrimary && oldPrimaryUserId.HasValue && oldPrimaryUserId.Value != userId)
            {
                prevPrimaryUser = await _dbContext.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.CenterId == centerId && u.UserId == oldPrimaryUserId.Value && !u.IsDeleted, ct);

                if (prevPrimaryUser is not null)
                {
                    // Enforce mandatory ExpectedPreviousPrimaryUserRowVersion when disabling previous primary
                    if (string.IsNullOrWhiteSpace(request.ExpectedPreviousPrimaryUserRowVersion) ||
                        !ulong.TryParse(request.ExpectedPreviousPrimaryUserRowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedPrevVer))
                    {
                        return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                            ErrorCodes.ValidationFailed, "ExpectedPreviousPrimaryUserRowVersion là bắt buộc khi vô hiệu hóa Quản lý chính trước đó.");
                    }

                    if (prevPrimaryUser.RowVersion != expectedPrevVer)
                    {
                        return PlatformResult<MakePrimaryCenterManagerData>.Failure(
                            ErrorCodes.ConcurrencyConflict, "Dữ liệu quản lý chính trước đó đã bị thay đổi bởi thao tác khác. Vui lòng tải lại.");
                    }

                    prevPrimaryUser.Status = UserStatus.Disabled;
                    prevPrimaryUser.RowVersion++;
                    prevPrimaryUser.AuthVersion++;
                    prevPrimaryUser.UpdatedAt = now;
                    prevPrimaryUser.UpdatedBy = callerUserId;

                    var prevTokens = await _dbContext.RefreshTokens
                        .IgnoreQueryFilters()
                        .Where(rt => rt.CenterId == centerId && rt.UserId == prevPrimaryUser.UserId && rt.RevokedAt == null)
                        .ToListAsync(ct);

                    foreach (var t in prevTokens)
                    {
                        t.RevokedAt = now;
                        t.RevokeReason = "Disabled after transferring primary manager.";
                    }
                }
            }

            center.PrimaryManagerUserId = userId;
            center.RowVersion++;
            center.UpdatedAt = now;

            RecordPlatformAudit(
                centerId,
                callerUserId,
                "Center",
                centerId.ToString("D"),
                "CenterPrimaryManagerChanged",
                new { PrimaryManagerUserId = oldPrimaryUserId },
                new
                {
                    PrimaryManagerUserId = userId,
                    PreviousPrimaryDisabled = request.DisablePreviousPrimary && prevPrimaryUser is not null
                },
                sanitizedReason,
                traceId,
                now);

            return PlatformResult<MakePrimaryCenterManagerData>.Success(new MakePrimaryCenterManagerData
            {
                CenterId = centerId,
                PrimaryManagerUserId = userId,
                NewCenterRowVersion = center.RowVersion.ToString(CultureInfo.InvariantCulture),
                PreviousPrimaryDisabled = request.DisablePreviousPrimary && prevPrimaryUser is not null,
                UpdatedAtUtc = now
            });
        }, cancellationToken);
    }

    private static Expression<Func<T, bool>> BuildOrEqualityFilter<T, TProp>(
        string propertyName,
        IReadOnlyList<TProp> values)
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var property = Expression.Property(parameter, propertyName);
        Expression? orBody = null;
        foreach (var val in values)
        {
            var equals = Expression.Equal(property, Expression.Constant(val, typeof(TProp)));
            orBody = orBody == null ? equals : Expression.OrElse(orBody, equals);
        }
        return Expression.Lambda<Func<T, bool>>(orBody!, parameter);
    }

    private static string NormalizeAscii(string? input, bool toUpper = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var text = input.Trim();
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                if (c == 'đ') sb.Append('d');
                else if (c == 'Đ') sb.Append('D');
                else sb.Append(c);
            }
        }
        var result = sb.ToString().Normalize(NormalizationForm.FormC);
        return toUpper ? result.ToUpperInvariant() : result;
    }
}
