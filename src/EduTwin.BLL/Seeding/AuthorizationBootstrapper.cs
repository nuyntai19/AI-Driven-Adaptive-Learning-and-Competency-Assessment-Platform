using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Seeding;

public sealed class AuthorizationBootstrapper(
    EduTwinDbContext dbContext,
    TimeProvider timeProvider)
{
    public static readonly Guid ReservedPlatformCenterId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        var centers = await dbContext.Centers
            .IgnoreQueryFilters()
            .Where(center => !center.IsDeleted && center.CenterId != ReservedPlatformCenterId)
            .Select(center => center.CenterId)
            .ToArrayAsync(cancellationToken);

        var permissions = await dbContext.PermissionAccountTypes
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        foreach (var centerId in centers)
        {
            await EnsureCenterAsync(centerId, permissions, cancellationToken);
        }
    }

    public async Task EnsureCenterAsync(
        Guid centerId,
        IReadOnlyCollection<PermissionAccountType>? permissionMappings = null,
        CancellationToken cancellationToken = default)
    {
        permissionMappings ??= await dbContext.PermissionAccountTypes
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var users = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => user.CenterId == centerId && !user.IsDeleted)
            .ToArrayAsync(cancellationToken);
        var actor = users
            .Where(user => user.RoleName == UserRole.CenterManager &&
                           user.Status == UserStatus.Active)
            .OrderBy(user => user.UserId)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Center {centerId:D} has no active CenterManager for authorization bootstrap.");

        var roles = await dbContext.AuthorizationRoles
            .IgnoreQueryFilters()
            .Where(role => role.CenterId == centerId && role.IsSystemRole)
            .ToListAsync(cancellationToken);
        var changed = false;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var accountType in Enum.GetValues<UserRole>())
        {
            if (accountType == UserRole.PlatformAdmin)
            {
                continue;
            }

            var role = roles.SingleOrDefault(item => item.AccountType == accountType);
            if (role is null)
            {
                role = CreateSystemRole(centerId, accountType, utcNow);
                dbContext.AuthorizationRoles.Add(role);
                roles.Add(role);
                changed = true;
            }

            var existingPermissionIds = await dbContext.RolePermissions
                .IgnoreQueryFilters()
                .Where(item => item.CenterId == centerId && item.RoleId == role.RoleId)
                .Select(item => item.PermissionId)
                .ToHashSetAsync(cancellationToken);
            foreach (var mapping in permissionMappings.Where(item => item.AccountType == accountType))
            {
                if (existingPermissionIds.Add(mapping.PermissionId))
                {
                    dbContext.RolePermissions.Add(new RolePermission
                    {
                        CenterId = centerId,
                        RoleId = role.RoleId,
                        PermissionId = mapping.PermissionId,
                        AccountType = accountType,
                        GrantedAt = utcNow,
                        GrantedByUserId = actor.UserId
                    });
                    changed = true;
                }
            }
        }

        var existingAssignments = await dbContext.UserRoleAssignments
            .IgnoreQueryFilters()
            .Where(item => item.CenterId == centerId)
            .Select(item => new { item.UserId, item.RoleId })
            .ToArrayAsync(cancellationToken);
        var assignmentKeys = existingAssignments
            .Select(item => (item.UserId, item.RoleId))
            .ToHashSet();
        foreach (var user in users)
        {
            if (user.RoleName == UserRole.PlatformAdmin)
            {
                continue;
            }

            var role = roles.Single(item => item.AccountType == user.RoleName);
            if (assignmentKeys.Add((user.UserId, role.RoleId)))
            {
                dbContext.UserRoleAssignments.Add(new UserRoleAssignment
                {
                    CenterId = centerId,
                    UserId = user.UserId,
                    RoleId = role.RoleId,
                    AccountType = user.RoleName,
                    Status = UserRoleAssignmentStatus.Active,
                    AssignedAt = utcNow,
                    AssignedByUserId = actor.UserId
                });
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = centerId,
            ActorUserId = null,
            ActionType = "AuthorizationBootstrap",
            TargetType = "Center",
            TargetId = centerId.ToString("D"),
            AfterData = "{\"catalogVersion\":\"v1\",\"systemRoles\":3}",
            Reason = "Khởi tạo phân quyền động cho dữ liệu seed hoặc Center mới.",
            TraceId = "runtime-seed:authorization-bootstrap",
            CreatedAt = utcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BootstrapPlatformAsync(
        Guid platformCenterId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var role = await dbContext.AuthorizationRoles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.CenterId == platformCenterId && r.AccountType == UserRole.PlatformAdmin && r.IsSystemRole, cancellationToken);

        if (role is null)
        {
            role = CreateSystemRole(platformCenterId, UserRole.PlatformAdmin, utcNow);
            dbContext.AuthorizationRoles.Add(role);
        }

        var platformPermissions = await dbContext.PermissionAccountTypes
            .IgnoreQueryFilters()
            .Where(p => p.AccountType == UserRole.PlatformAdmin)
            .Select(p => p.PermissionId)
            .ToArrayAsync(cancellationToken);

        var existingRolePermissions = await dbContext.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.CenterId == platformCenterId && rp.RoleId == role.RoleId)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync(cancellationToken);

        foreach (var permId in platformPermissions)
        {
            if (existingRolePermissions.Add(permId))
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    CenterId = platformCenterId,
                    RoleId = role.RoleId,
                    PermissionId = permId,
                    AccountType = UserRole.PlatformAdmin,
                    GrantedAt = utcNow,
                    GrantedByUserId = adminUserId
                });
            }
        }

        var assignmentExists = await dbContext.UserRoleAssignments
            .IgnoreQueryFilters()
            .AnyAsync(a => a.CenterId == platformCenterId && a.UserId == adminUserId && a.RoleId == role.RoleId, cancellationToken);

        if (!assignmentExists)
        {
            dbContext.UserRoleAssignments.Add(new UserRoleAssignment
            {
                CenterId = platformCenterId,
                UserId = adminUserId,
                RoleId = role.RoleId,
                AccountType = UserRole.PlatformAdmin,
                Status = UserRoleAssignmentStatus.Active,
                AssignedAt = utcNow,
                AssignedByUserId = adminUserId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuthorizationRole CreateSystemRole(
        Guid centerId,
        UserRole accountType,
        DateTime utcNow)
    {
        var roleName = accountType switch
        {
            UserRole.Student => "Học viên hệ thống",
            UserRole.Teacher => "Giáo viên hệ thống",
            UserRole.CenterManager => "Quản trị trung tâm",
            UserRole.PlatformAdmin => "Quản trị viên nền tảng",
            _ => throw new ArgumentOutOfRangeException(nameof(accountType))
        };

        return new AuthorizationRole
        {
            RoleId = AuthorizationPermissionCatalog.CreateSystemRoleId(centerId, accountType),
            RoleCode = $"SYSTEM_{accountType.ToString().ToUpperInvariant()}",
            RoleName = roleName,
            AccountType = accountType,
            Description = "Vai trò hệ thống được tạo khi chuyển đổi sang phân quyền động.",
            IsSystemRole = true,
            Status = AuthorizationRoleStatus.Active,
            CenterId = centerId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }
}
