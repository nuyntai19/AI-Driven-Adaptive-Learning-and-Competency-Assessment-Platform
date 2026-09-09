using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;

namespace EduTwin.API.Security;

public sealed class PermissionAuthorizationHandler(
    IPermissionEvaluator permissionEvaluator)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (await permissionEvaluator.HasPermissionAsync(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
