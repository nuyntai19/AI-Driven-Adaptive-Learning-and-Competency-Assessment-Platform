using System.Text.Json;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.IdentityAndTenancy;

public static class NewAccountRoleProvisioning
{
    // Called only for a new account. Never reactivates a revoked assignment or
    // reconciles other users/role permissions as a side effect of account creation.
    public static async Task<bool> AddAsync(EduTwinDbContext db, User user,
        Guid actorId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var role = await db.AuthorizationRoles.AsNoTracking()
            .SingleOrDefaultAsync(r => r.CenterId == user.CenterId &&
                r.AccountType == user.RoleName && r.IsSystemRole && !r.IsDeleted &&
                r.Status == AuthorizationRoleStatus.Active, cancellationToken);
        if (role is null) return false; // Fail closed: do not create an unusable account.

        db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            CenterId = user.CenterId, UserId = user.UserId, RoleId = role.RoleId,
            AccountType = user.RoleName, Status = UserRoleAssignmentStatus.Active,
            AssignedAt = utcNow, AssignedByUserId = actorId
        });
        db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = user.CenterId, ActorUserId = actorId, TargetUserId = user.UserId,
            TargetType = "User", TargetId = user.UserId.ToString("D"),
            ActionType = "UserSystemRoleAssigned",
            AfterData = JsonSerializer.Serialize(new { role.RoleId, role.RoleCode, AccountType = user.RoleName.ToString() }),
            Reason = "Gán vai trò hệ thống đúng loại tài khoản khi tạo tài khoản mới.",
            TraceId = "account-creation:system-role", CreatedAt = utcNow, CreatedBy = actorId
        });
        return true;
    }
}
