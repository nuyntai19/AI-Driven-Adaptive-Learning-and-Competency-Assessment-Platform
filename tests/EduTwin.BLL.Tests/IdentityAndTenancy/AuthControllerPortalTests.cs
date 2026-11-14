using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.Controllers;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

/// <summary>The platform tenant signs in only through /admin; the center portal never accepts it.</summary>
public class AuthControllerPortalTests
{
    private readonly Mock<ILoginUseCase> _login = new();

    private AuthController CreateController()
    {
        var controller = new AuthController(
            _login.Object,
            Mock.Of<IRefreshUseCase>(),
            Mock.Of<ILogoutUseCase>(),
            Mock.Of<IGetCurrentUserUseCase>(),
            TimeProvider.System,
            Mock.Of<IWebHostEnvironment>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
        return controller;
    }

    private void LoginFails() => _login
        .Setup(x => x.ExecuteAsync(It.IsAny<LoginRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new LoginResult { IsSuccess = false, ErrorCode = ErrorCodes.AuthInvalidCredentials });

    [Theory]
    [InlineData("PLATFORM")]
    [InlineData("platform")]
    [InlineData("Platform")]
    public async Task CenterPortal_RejectsPlatformTenant_WithoutCheckingCredentials(string centerCode)
    {
        var result = await CreateController().Login(new LoginRequest { CenterCode = centerCode, Username = "platform.admin", Password = "secret" }, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _login.Verify(x => x.ExecuteAsync(It.IsAny<LoginRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CenterPortal_PassesCenterCredentialsThrough()
    {
        LoginFails();
        await CreateController().Login(new LoginRequest { CenterCode = "EDUTWIN_A", Username = "student01", Password = "secret" }, CancellationToken.None);

        _login.Verify(x => x.ExecuteAsync(It.Is<LoginRequest>(r => r.CenterCode == "EDUTWIN_A" && r.Username == "student01"), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdminPortal_AlwaysSignsIntoPlatformTenant()
    {
        LoginFails();
        var result = await CreateController().AdminLogin(new AdminLoginRequest { Username = " platform.admin ", Password = "secret" }, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _login.Verify(x => x.ExecuteAsync(It.Is<LoginRequest>(r => r.CenterCode == AuthController.PlatformCenterCode && r.Username == "platform.admin" && r.Password == "secret"), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class TestProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new() { Status = statusCode, Title = title, Type = type, Detail = detail, Instance = instance };

        public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext, ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null, string? type = null, string? detail = null, string? instance = null)
            => new(modelStateDictionary) { Status = statusCode, Title = title, Type = type, Detail = detail, Instance = instance };
    }
}
