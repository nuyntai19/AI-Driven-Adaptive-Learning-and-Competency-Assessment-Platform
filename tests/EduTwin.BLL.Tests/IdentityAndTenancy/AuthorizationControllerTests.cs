using System.Reflection;
using EduTwin.API.Controllers;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class AuthorizationControllerTests
{
    [Fact]
    public async Task GetPermissions_Success_ReturnsPagedResponseAndExactToken()
    {
        var useCase = new Mock<IListPermissionsUseCase>();
        useCase.Setup(item => item.ExecuteAsync(
                It.IsAny<PermissionListQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListPermissionsResult.Success(
                [new PermissionDto { PermissionCode = "authorization.permissions.read" }],
                1,
                1));
        var controller = new AuthorizationController(
            useCase.Object,
            TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        var query = new PermissionListQuery();
        using var source = new CancellationTokenSource();

        var action = await controller.GetPermissions(query, source.Token);

        var ok = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<PermissionListResponse>(ok.Value);
        Assert.Single(response.Data);
        Assert.Equal(1, response.Meta.TotalItems);
        useCase.Verify(item => item.ExecuteAsync(query, source.Token), Times.Once);
    }

    [Fact]
    public void GetPermissions_RequiresExactDynamicPermissionPolicy()
    {
        var method = typeof(AuthorizationController).GetMethod(
            nameof(AuthorizationController.GetPermissions));

        var attribute = Assert.Single(
            method!.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("authorization.permissions.read", attribute.Policy);
    }
}
