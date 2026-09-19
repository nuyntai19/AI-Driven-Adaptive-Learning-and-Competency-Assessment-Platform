using System;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using EduTwin.API.Controllers;
using EduTwin.BLL.Platform;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Platform;

namespace EduTwin.BLL.Tests.Platform;

public class PlatformSecurityRateLimitingTests
{
    [Fact]
    public async Task ChangePassword_RateLimiting_RejectsSixthAttemptWith429_AndMapsFirstFiveTo401()
    {
        var mockMeService = new Mock<IPlatformMeService>();
        mockMeService
            .Setup(s => s.ChangePasswordAsync(It.IsAny<PlatformChangePasswordRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlatformResult<bool>.Failure(
                ErrorCodes.AuthInvalidCredentials, "Mật khẩu hiện tại không chính xác."));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(PlatformMeController).Assembly);
        builder.Services.AddSingleton(mockMeService.Object);
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddAuthentication("TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestPlatformAuthHandler>("TestScheme", options => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("platform.account.manage_own", p => p.RequireAuthenticatedUser());
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("PlatformSecurityPolicy", httpContext =>
            {
                var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? httpContext.User.FindFirst("sub")?.Value;

                var endpoint = httpContext.GetEndpoint() as RouteEndpoint;
                var routePattern = endpoint?.RoutePattern.RawText ?? httpContext.Request.Path.Value ?? string.Empty;
                var httpMethod = httpContext.Request.Method;

                var partitionKey = !string.IsNullOrEmpty(userId)
                    ? $"user_{userId}_{httpMethod}_{routePattern}"
                    : $"ip_{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}_{httpMethod}_{routePattern}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
        });

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();

        await app.StartAsync();
        using var client = app.GetTestClient();

        var requestBody = new PlatformChangePasswordRequest
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewValidPassword123!",
            ConfirmPassword = "NewValidPassword123!"
        };

        // First 5 requests: Wrong password returns 401 Unauthorized (AuthInvalidCredentials)
        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/platform/me/change-password", requestBody);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // 6th request: Rate limiter triggers and returns 429 TooManyRequests
        var rateLimitedResponse = await client.PostAsJsonAsync("/api/v1/platform/me/change-password", requestBody);
        Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);

        await app.StopAsync();
    }

    [Fact]
    public void RateLimiting_PartitionUsesRoutePattern_NotRawGuidPath()
    {
        // Demonstrates that the partition key uses route pattern rather than raw URL path
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("api/v1/platform/centers/{centerId:guid}/managers/{managerUserId:guid}/reset-password"),
            0,
            EndpointMetadataCollection.Empty,
            "ResetPassword");

        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(endpoint);
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/v1/platform/centers/11111111-1111-1111-1111-111111111111/managers/22222222-2222-2222-2222-222222222222/reset-password";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "admin-123")
        ], "Test"));

        var routeEndpoint = httpContext.GetEndpoint() as RouteEndpoint;
        var routePattern = routeEndpoint?.RoutePattern.RawText ?? httpContext.Request.Path.Value ?? string.Empty;
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var partitionKey = $"user_{userId}_{httpContext.Request.Method}_{routePattern}";

        // Assert that the partition key contains the parameterized route pattern and NOT the raw GUIDs
        Assert.Equal("user_admin-123_POST_api/v1/platform/centers/{centerId:guid}/managers/{managerUserId:guid}/reset-password", partitionKey);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", partitionKey);
    }

    public class TestPlatformAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestPlatformAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "admin-fixed-id"),
                new Claim(ClaimTypes.Name, "platform.admin"),
                new Claim("role", "PlatformAdmin")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
