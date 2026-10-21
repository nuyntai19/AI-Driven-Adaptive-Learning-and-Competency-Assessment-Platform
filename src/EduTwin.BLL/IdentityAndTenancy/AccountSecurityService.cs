using EduTwin.BLL.Platform;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Identity;

namespace EduTwin.BLL.IdentityAndTenancy;

// The platform controller keeps its platform-only policy and service guard.
// Tenant self-service accepts no target user or center from the request.
public sealed class AccountSecurityService : PlatformMeService
{
    private readonly ITenantContext tenant;
    public AccountSecurityService(EduTwinDbContext db, ITenantContext tenant, IPasswordHasher<User> hasher, TimeProvider clock)
        : base(db, tenant, hasher, clock) => this.tenant = tenant;

    protected override bool IsAuthorizedAccount(out Guid callerUserId)
    {
        callerUserId = Guid.Empty;
        if (!tenant.IsResolved || !tenant.UserId.HasValue || !tenant.CenterId.HasValue ||
            tenant.CenterId == Guid.Empty || tenant.CenterId == AuthorizationBootstrapper.ReservedPlatformCenterId ||
            !Enum.TryParse<UserRole>(tenant.Role, out var role) ||
            role is not (UserRole.Student or UserRole.Teacher or UserRole.CenterManager)) return false;
        callerUserId = tenant.UserId.Value;
        return true;
    }
}
