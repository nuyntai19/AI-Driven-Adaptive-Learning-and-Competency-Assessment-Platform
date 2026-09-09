using System.Security.Claims;
using System.Text.Json;
using EduTwin.API.Middleware;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class AuthorizationVersionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_MatchingVersion_ContinuesPipeline()
    {
        var fixture = await CreateFixtureAsync(databaseVersion: 4, tokenVersion: 4);
        var nextCalled = false;
        var sut = new AuthorizationVersionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(
            fixture.HttpContext,
            fixture.TenantContext.Object,
            fixture.DbContext);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, fixture.HttpContext.Response.StatusCode);
        await fixture.DbContext.DisposeAsync();
    }

    [Fact]
    public async Task InvokeAsync_StaleVersion_ReturnsContractProblemAndStopsPipeline()
    {
        var fixture = await CreateFixtureAsync(databaseVersion: 5, tokenVersion: 4);
        var nextCalled = false;
        var sut = new AuthorizationVersionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(
            fixture.HttpContext,
            fixture.TenantContext.Object,
            fixture.DbContext);

        Assert.False(nextCalled);
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            fixture.HttpContext.Response.StatusCode);
        fixture.HttpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            fixture.HttpContext.Response.Body);
        Assert.Equal(
            ErrorCodes.AuthorizationVersionStale,
            document.RootElement.GetProperty("errorCode").GetString());
        await fixture.DbContext.DisposeAsync();
    }

    private static async Task<MiddlewareFixture> CreateFixtureAsync(
        uint databaseVersion,
        uint tokenVersion)
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessor.SetupGet(item => item.CenterId).Returns(centerId);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new EduTwinDbContext(options, accessor.Object);
        dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "MIDDLEWARE",
            CenterName = "Middleware center",
            Status = CenterStatus.Active,
            Timezone = "Asia/Bangkok",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        dbContext.Users.Add(new User
        {
            UserId = userId,
            CenterId = centerId,
            Username = "middleware-user",
            PasswordHash = "not-used",
            RoleName = UserRole.CenterManager,
            DisplayName = "Middleware User",
            Status = UserStatus.Active,
            AuthVersion = databaseVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var tenantContext = new Mock<ITenantContext>();
        tenantContext.SetupGet(item => item.IsResolved).Returns(true);
        tenantContext.SetupGet(item => item.CenterId).Returns(centerId);
        tenantContext.SetupGet(item => item.UserId).Returns(userId);
        tenantContext.SetupGet(item => item.AuthVersion).Returns(tokenVersion);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim("sub", userId.ToString("D"))],
                "Bearer"));
        httpContext.Response.Body = new MemoryStream();

        return new MiddlewareFixture(httpContext, tenantContext, dbContext);
    }

    private sealed record MiddlewareFixture(
        DefaultHttpContext HttpContext,
        Mock<ITenantContext> TenantContext,
        EduTwinDbContext DbContext);
}
