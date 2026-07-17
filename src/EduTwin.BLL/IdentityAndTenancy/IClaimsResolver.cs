using System.Security.Claims;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IClaimsResolver
{
    void Resolve(ClaimsPrincipal principal, ITenantContextInitializer initializer);
}
