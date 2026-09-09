using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;

namespace EduTwin.API.Security;

public sealed class AnyPermissionAuthorizationHandler(
    IPermissionEvaluator permissionEvaluator)
    : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        foreach (var permissionCode in requirement.PermissionCodes)
        {
            if (await permissionEvaluator.HasPermissionAsync(permissionCode))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
