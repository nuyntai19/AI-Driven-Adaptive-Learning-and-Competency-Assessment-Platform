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
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        var centers = await dbContext.Centers
            .IgnoreQueryFilters()
            .Where(center => !center.IsDeleted)
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

    private async Task EnsureCenterAsync(
        Guid centerId,
        IReadOnlyCollection<PermissionAccountType> permissionMappings,
        CancellationToken cancellationToken)
    {
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
