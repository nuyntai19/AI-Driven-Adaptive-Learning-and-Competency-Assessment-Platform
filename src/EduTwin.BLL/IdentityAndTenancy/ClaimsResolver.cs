using System;
using System.Linq;
using System.Security.Claims;

namespace EduTwin.BLL.IdentityAndTenancy;

public class ClaimsResolver : IClaimsResolver
{
    public void Resolve(ClaimsPrincipal principal, ITenantContextInitializer initializer)
    {
        if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
        {
            return;
        }

        var claims = principal.Claims.ToList();

        var subClaims = claims.Where(c => c.Type == "sub").ToList();
        var centerIdClaims = claims.Where(c => c.Type == "center_id").ToList();
        var roleClaims = claims.Where(c => c.Type == "role").ToList();
        var authVersionClaims = claims.Where(c => c.Type == "auth_version").ToList();

        if (subClaims.Count != 1)
            throw new UnauthorizedAccessException("Missing or duplicate sub claim.");
        if (centerIdClaims.Count != 1)
            throw new UnauthorizedAccessException("Missing or duplicate center_id claim.");
        if (roleClaims.Count != 1)
            throw new UnauthorizedAccessException("Missing or duplicate role claim.");
        if (authVersionClaims.Count != 1)
            throw new UnauthorizedAccessException("Missing or duplicate auth_version claim.");

        var subClaim = subClaims[0].Value;
        var centerIdClaim = centerIdClaims[0].Value;
        var roleClaim = roleClaims[0].Value;
        var authVersionClaim = authVersionClaims[0].Value;

        if (!Guid.TryParse(centerIdClaim, out var centerId) || centerId == Guid.Empty)
            throw new UnauthorizedAccessException("Missing or invalid center_id claim.");

        if (!Guid.TryParse(subClaim, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedAccessException("Missing or invalid sub claim.");

        if (string.IsNullOrWhiteSpace(roleClaim))
            throw new UnauthorizedAccessException("Missing or invalid role claim.");

        if (!int.TryParse(authVersionClaim, out var authVersion) || authVersion < 1)
            throw new UnauthorizedAccessException("Missing or invalid auth_version claim.");

        initializer.Initialize(centerId, userId, roleClaim, authVersion);
    }

}
