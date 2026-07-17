using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using EduTwin.BLL.IdentityAndTenancy;

namespace EduTwin.API.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IClaimsResolver claimsResolver, ITenantContextInitializer tenantContextInitializer)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            claimsResolver.Resolve(context.User, tenantContextInitializer);
        }

        await _next(context);
    }
}
