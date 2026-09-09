using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.API.Middleware;

public sealed class AuthorizationVersionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        EduTwinDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tokenVersion = tenantContext.AuthVersion;
            var userId = tenantContext.UserId;
            if (!tenantContext.IsResolved ||
                !userId.HasValue ||
                userId.Value == Guid.Empty ||
                !tokenVersion.HasValue ||
                tokenVersion.Value < 1)
            {
                await WriteStaleResponseAsync(context);
                return;
            }

            var currentVersion = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.UserId == userId.Value)
                .Select(user => (uint?)user.AuthVersion)
                .SingleOrDefaultAsync(context.RequestAborted);
            if (currentVersion is null || currentVersion.Value != tokenVersion.Value)
            {
                await WriteStaleResponseAsync(context);
                return;
            }
        }

        await next(context);
    }

    private static async Task WriteStaleResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = "https://edutwin.local/problems/authorization-version-stale",
            Title = "Phiên phân quyền đã thay đổi",
            Status = StatusCodes.Status401Unauthorized,
            Detail = "Quyền của tài khoản đã thay đổi. Vui lòng đăng nhập lại.",
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        problem.Extensions["errorCode"] = ErrorCodes.AuthorizationVersionStale;
        await context.Response.WriteAsJsonAsync(problem);
    }
}
