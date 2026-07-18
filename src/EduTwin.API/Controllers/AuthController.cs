using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Common;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILoginUseCase _loginUseCase;
    private readonly IRefreshUseCase _refreshUseCase;
    private readonly ILogoutUseCase _logoutUseCase;
    private readonly IGetCurrentUserUseCase _getCurrentUserUseCase;
    private readonly TimeProvider _timeProvider;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        ILoginUseCase loginUseCase,
        IRefreshUseCase refreshUseCase,
        ILogoutUseCase logoutUseCase,
        IGetCurrentUserUseCase getCurrentUserUseCase,
        TimeProvider timeProvider,
        IWebHostEnvironment env)
    {
        _loginUseCase = loginUseCase;
        _refreshUseCase = refreshUseCase;
        _logoutUseCase = logoutUseCase;
        _getCurrentUserUseCase = getCurrentUserUseCase;
        _timeProvider = timeProvider;
        _env = env;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _loginUseCase.ExecuteAsync(request, clientIp, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == ErrorCodes.AuthInvalidCredentials)
            {
                return Problem(
                    type: "https://edutwin.local/problems/auth-invalid-credentials",
                    title: "Thông tin đăng nhập không hợp lệ",
                    statusCode: StatusCodes.Status401Unauthorized,
                    detail: "Mã trung tâm, tên đăng nhập hoặc mật khẩu không hợp lệ.",
                    instance: HttpContext.Request.Path,
                    extensions: new System.Collections.Generic.Dictionary<string, object?>
                    {
                        { "errorCode", ErrorCodes.AuthInvalidCredentials }
                    });
            }
            if (result.ErrorCode == ErrorCodes.AuthUserDisabled)
            {
                return Problem(
                    type: "https://edutwin.local/problems/auth-user-disabled",
                    title: "Tài khoản không khả dụng",
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "Tài khoản hoặc trung tâm hiện không khả dụng.",
                    instance: HttpContext.Request.Path,
                    extensions: new System.Collections.Generic.Dictionary<string, object?>
                    {
                        { "errorCode", ErrorCodes.AuthUserDisabled }
                    });
            }
            throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
        }

        AppendRefreshCookie(result.RawRefreshToken!, result.RefreshTokenExpiresAt);

        var response = new LoginResponse
        {
            Data = result.Data!,
            Meta = new MetaDto
            {
                TraceId = HttpContext.TraceIdentifier,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        };

        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var rawToken = Request.Cookies["edutwin_refresh"];
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _refreshUseCase.ExecuteAsync(rawToken, clientIp, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == ErrorCodes.AuthRefreshInvalid)
            {
                return Problem(
                    type: "https://edutwin.local/problems/auth-refresh-invalid",
                    title: "Phiên đăng nhập không hợp lệ hoặc đã hết hạn",
                    statusCode: StatusCodes.Status401Unauthorized,
                    detail: "Phiên đăng nhập không hợp lệ, vui lòng đăng nhập lại.",
                    instance: HttpContext.Request.Path,
                    extensions: new System.Collections.Generic.Dictionary<string, object?>
                    {
                        { "errorCode", ErrorCodes.AuthRefreshInvalid }
                    });
            }
            if (result.ErrorCode == ErrorCodes.AuthUserDisabled)
            {
                return Problem(
                    type: "https://edutwin.local/problems/auth-user-disabled",
                    title: "Tài khoản không khả dụng",
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "Tài khoản hoặc trung tâm hiện không khả dụng.",
                    instance: HttpContext.Request.Path,
                    extensions: new System.Collections.Generic.Dictionary<string, object?>
                    {
                        { "errorCode", ErrorCodes.AuthUserDisabled }
                    });
            }
            throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
        }

        AppendRefreshCookie(result.RawRefreshToken!, result.RefreshTokenExpiresAt);

        var response = new LoginResponse
        {
            Data = result.Data!,
            Meta = new MetaDto
            {
                TraceId = HttpContext.TraceIdentifier,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        };

        return Ok(response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var rawToken = Request.Cookies["edutwin_refresh"];
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (!string.IsNullOrEmpty(rawToken))
        {
            await _logoutUseCase.ExecuteAsync(rawToken, clientIp, cancellationToken);
        }

        DeleteRefreshCookie();

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var result = await _getCurrentUserUseCase.ExecuteAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorCode == ErrorCodes.ResourceNotFound)
            {
                return Problem(
                    type: "https://edutwin.local/problems/resource-not-found",
                    title: "Không tìm thấy dữ liệu",
                    statusCode: StatusCodes.Status404NotFound,
                    detail: "Không tìm thấy người dùng hiện tại.",
                    instance: HttpContext.Request.Path,
                    extensions: new System.Collections.Generic.Dictionary<string, object?>
                    {
                        { "errorCode", ErrorCodes.ResourceNotFound }
                    });
            }
            if (result.ErrorCode == ErrorCodes.AuthUserDisabled)
            {
                return Problem(
                    type: "https://edutwin.local/problems/auth-user-disabled",
                    title: "Tài khoản không khả dụng",
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "Tài khoản hoặc trung tâm hiện không khả dụng.",
                    instance: HttpContext.Request.Path,
                    extensions: new System.Collections.Generic.Dictionary<string, object?>
                    {
                        { "errorCode", ErrorCodes.AuthUserDisabled }
                    });
            }
            throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
        }

        var response = new CurrentUserResponse
        {
            Data = result.Data!,
            Meta = new MetaDto
            {
                TraceId = HttpContext.TraceIdentifier,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        };

        return Ok(response);
    }

    private CookieOptions CreateRefreshCookieOptions(DateTimeOffset? expires = null)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Secure = _env.EnvironmentName != "Development"
        };
        if (expires.HasValue)
        {
            options.Expires = expires.Value;
        }
        return options;
    }

    private void AppendRefreshCookie(string token, DateTime? expires)
    {
        Response.Cookies.Append("edutwin_refresh", token, CreateRefreshCookieOptions(expires));
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete("edutwin_refresh", CreateRefreshCookieOptions());
    }
}
