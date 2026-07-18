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
    private readonly TimeProvider _timeProvider;
    private readonly IWebHostEnvironment _env;

    public AuthController(ILoginUseCase loginUseCase, TimeProvider timeProvider, IWebHostEnvironment env)
    {
        _loginUseCase = loginUseCase;
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

        // Set refresh cookie
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Secure = true,
            Expires = result.RefreshTokenExpiresAt
        };

        if (_env.EnvironmentName == "Development")
        {
            cookieOptions.Secure = false;
        }
        else
        {
            cookieOptions.Secure = true;
        }

        Response.Cookies.Append("edutwin_refresh", result.RawRefreshToken!, cookieOptions);

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
}
