using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EduTwin.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/account/me")]
public sealed class AccountSecurityController(AccountSecurityService service) : ControllerBase
{
    [HttpGet("security")]
    public async Task<IActionResult> Security(CancellationToken ct)
    {
        var result = await service.GetSecurityProfileAsync(HttpContext.TraceIdentifier, ct);
        return result.IsSuccess ? Ok(new { data = result.Data }) : Failure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> ChangePassword(PlatformChangePasswordRequest request, CancellationToken ct)
    {
        var result = await service.ChangePasswordAsync(request, HttpContext.TraceIdentifier, ct);
        return result.IsSuccess ? Ok(new { data = new { success = true, message = "Đã đổi mật khẩu và thu hồi toàn bộ phiên. Vui lòng đăng nhập lại." } })
            : Failure(result.ErrorCode, result.ErrorMessage);
    }

    [HttpPost("revoke-sessions")]
    [EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> RevokeSessions(PlatformRevokeSessionsRequest? request, CancellationToken ct)
    {
        var result = await service.RevokeSessionsAsync(request, HttpContext.TraceIdentifier, ct);
        return result.IsSuccess ? Ok(new { data = new { success = true, message = "Đã thu hồi toàn bộ phiên, gồm phiên hiện tại. Vui lòng đăng nhập lại." } })
            : Failure(result.ErrorCode, result.ErrorMessage);
    }

    private ObjectResult Failure(string? code, string? message) => StatusCode(code switch
    {
        ErrorCodes.ForbiddenResource => 403,
        ErrorCodes.ResourceNotFound => 404,
        ErrorCodes.ConcurrencyConflict => 409,
        _ => 400 // Wrong current password is validation, not an expired session.
    }, new ProblemDetails { Detail = message, Extensions = { ["errorCode"] = code ?? "ValidationFailed" } });
}
