using System.Security.Claims;
using EduTwin.API.Security;
using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class AnyPermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenSecondPermissionIsGranted_Succeeds()
    {
        var evaluator = new Mock<IPermissionEvaluator>(MockBehavior.Strict);
        evaluator
            .Setup(item => item.HasPermissionAsync("permission.first", default))
            .ReturnsAsync(false);
        evaluator
            .Setup(item => item.HasPermissionAsync("permission.second", default))
            .ReturnsAsync(true);
        var requirement = new AnyPermissionRequirement(
            ["permission.first", "permission.second"]);
        var context = CreateContext(requirement, authenticated: true);
        var sut = new AnyPermissionAuthorizationHandler(evaluator.Object);

        await sut.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        evaluator.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_WhenNoPermissionIsGranted_FailsClosed()
    {
        var evaluator = new Mock<IPermissionEvaluator>(MockBehavior.Strict);
        evaluator
            .Setup(item => item.HasPermissionAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);
        var requirement = new AnyPermissionRequirement(
            ["permission.first", "permission.second"]);
        var context = CreateContext(requirement, authenticated: true);
        var sut = new AnyPermissionAuthorizationHandler(evaluator.Object);

        await sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        evaluator.Verify(
            item => item.HasPermissionAsync(It.IsAny<string>(), default),
            Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WhenIdentityIsUnauthenticated_DoesNotQueryPermissions()
    {
        var evaluator = new Mock<IPermissionEvaluator>(MockBehavior.Strict);
        var requirement = new AnyPermissionRequirement(["permission.first"]);
        var context = CreateContext(requirement, authenticated: false);
        var sut = new AnyPermissionAuthorizationHandler(evaluator.Object);

        await sut.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        evaluator.VerifyNoOtherCalls();
    }

    [Fact]
    public void CompositePolicies_ContainOnlyCatalogPermissions()
    {
        var catalog = EduTwin.DAL.Seeding.AuthorizationPermissionCatalog
            .CreatePermissions()
            .Select(item => item.PermissionCode)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            CompositePermissionPolicies.GetAnyPermissionPolicies(),
            policy => Assert.All(
                policy.Value,
                permissionCode => Assert.Contains(permissionCode, catalog)));
    }

    private static AuthorizationHandlerContext CreateContext(
        IAuthorizationRequirement requirement,
        bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "Test")
            : new ClaimsIdentity();
        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(identity),
            resource: null);
    }
}
