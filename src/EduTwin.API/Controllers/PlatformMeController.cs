using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.Platform;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/platform/me")]
[Authorize(Policy = "platform.account.manage_own")]
public class PlatformMeController : ControllerBase
{
    private readonly IPlatformMeService _platformMeService;
    private readonly TimeProvider _timeProvider;

    public PlatformMeController(
        IPlatformMeService platformMeService,
        TimeProvider timeProvider)
    {
        _platformMeService = platformMeService ?? throw new ArgumentNullException(nameof(platformMeService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    [HttpGet("security")]
    [Authorize(Policy = "platform.account.manage_own")]
    public async Task<IActionResult> GetSecurityProfile(CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _platformMeService.GetSecurityProfileAsync(traceId, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapErrorToResponse(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new
        {
            data = result.Data,
            meta = new
            {
                traceId,
                timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    [HttpPost("change-password")]
    [Authorize(Policy = "platform.account.manage_own")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] PlatformChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _platformMeService.ChangePasswordAsync(request, traceId, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapErrorToResponse(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new
        {
            data = new { success = true, message = "Đổi mật khẩu thành công. Toàn bộ phiên làm việc cũ đã bị thu hồi." },
            meta = new
            {
                traceId,
                timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    [HttpPost("revoke-sessions")]
    [Authorize(Policy = "platform.account.manage_own")]
    public async Task<IActionResult> RevokeSessions(
        [FromBody] PlatformRevokeSessionsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _platformMeService.RevokeSessionsAsync(request, traceId, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapErrorToResponse(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new
        {
            data = new { success = true, message = "Đã thu hồi toàn bộ phiên đăng nhập thành công." },
            meta = new
            {
                traceId,
                timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    private IActionResult MapErrorToResponse(string? errorCode, string? errorMessage)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var message = errorMessage ?? "Đã xảy ra lỗi không xác định.";

        return errorCode switch
        {
            ErrorCodes.ValidationFailed => BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                Title = "Validation Failed",
                Status = StatusCodes.Status400BadRequest,
                Detail = message,
                Extensions = { ["errorCode"] = errorCode, ["traceId"] = traceId }
            }),
            ErrorCodes.ForbiddenResource => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = message,
                Extensions = { ["errorCode"] = errorCode, ["traceId"] = traceId }
            }),
            ErrorCodes.ResourceNotFound => NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = message,
                Extensions = { ["errorCode"] = errorCode, ["traceId"] = traceId }
            }),
            ErrorCodes.ConcurrencyConflict => Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                Title = "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = message,
                Extensions = { ["errorCode"] = errorCode, ["traceId"] = traceId }
            }),
            ErrorCodes.AuthInvalidCredentials => Unauthorized(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = message,
                Extensions = { ["errorCode"] = errorCode, ["traceId"] = traceId }
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = message,
                Extensions = { ["errorCode"] = errorCode ?? ErrorCodes.ValidationFailed, ["traceId"] = traceId }
            })
        };
    }
}
