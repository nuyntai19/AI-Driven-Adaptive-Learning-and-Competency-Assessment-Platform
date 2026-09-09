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
        var controller = CreateController(permissions: useCase.Object);
        var query = new PermissionListQuery();
        using var source = new CancellationTokenSource();

        var action = await controller.GetPermissions(query, source.Token);

        var ok = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<PermissionListResponse>(ok.Value);
        Assert.Single(response.Data);
        Assert.Equal(1, response.Meta.TotalItems);
        useCase.Verify(item => item.ExecuteAsync(query, source.Token), Times.Once);
    }

    [Theory]
    [InlineData(nameof(AuthorizationController.GetPermissions), "authorization.permissions.read")]
    [InlineData(nameof(AuthorizationController.GetRoles), "authorization.roles.read")]
    [InlineData(nameof(AuthorizationController.GetRole), "authorization.roles.read")]
    [InlineData(nameof(AuthorizationController.CreateRole), "authorization.roles.create")]
    [InlineData(nameof(AuthorizationController.UpdateRole), "authorization.roles.update")]
    [InlineData(nameof(AuthorizationController.ReplaceRolePermissions), "authorization.roles.manage_permissions")]
    [InlineData(nameof(AuthorizationController.GetUserRoles), "authorization.user_roles.read")]
    [InlineData(nameof(AuthorizationController.ReplaceUserRoles), "authorization.user_roles.assign")]
    [InlineData(nameof(AuthorizationController.GetAudit), "authorization.audit.read")]
    public void Endpoint_RequiresExactDynamicPermissionPolicy(
        string methodName,
        string expectedPolicy)
    {
        var method = typeof(AuthorizationController).GetMethod(
            methodName);

        var attribute = Assert.Single(
            method!.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(expectedPolicy, attribute.Policy);
    }

    [Fact]
    public async Task GetRoles_Success_ReturnsPagedResponseAndExactToken()
    {
        var useCase = new Mock<IListAuthorizationRolesUseCase>();
        useCase.Setup(item => item.ExecuteAsync(
                It.IsAny<AuthorizationRoleListQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListAuthorizationRolesResult.Success(
                [new AuthorizationRoleDto { RoleCode = "TEACHER" }],
                1,
                1));
        var controller = CreateController(roles: useCase.Object);
        var query = new AuthorizationRoleListQuery();
        using var source = new CancellationTokenSource();

        var action = await controller.GetRoles(query, source.Token);

        var ok = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<AuthorizationRoleListResponse>(ok.Value);
        Assert.Single(response.Data);
        Assert.Equal(1, response.Meta.TotalItems);
        useCase.Verify(item => item.ExecuteAsync(query, source.Token), Times.Once);
    }

    [Fact]
    public async Task ReplaceUserRoles_Success_PassesTraceAndCancellationToken()
    {
        var useCase = new Mock<IReplaceUserRolesUseCase>();
        useCase.Setup(item => item.ExecuteAsync(
                It.IsAny<Guid>(),
                It.IsAny<ReplaceUserRolesRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserAuthorizationResult.Success(new UserAuthorizationDto()));
        var controller = CreateController(replaceUserRoles: useCase.Object);
        var userId = Guid.NewGuid();
        var request = new ReplaceUserRolesRequest
        {
            RowVersion = "1",
            Reason = "Test"
        };
        using var source = new CancellationTokenSource();

        var action = await controller.ReplaceUserRoles(
            userId,
            request,
            source.Token);

        Assert.IsType<OkObjectResult>(action);
        useCase.Verify(item => item.ExecuteAsync(
            userId,
            request,
            It.Is<string>(trace => !string.IsNullOrWhiteSpace(trace)),
            source.Token), Times.Once);
    }

    private static AuthorizationController CreateController(
        IListPermissionsUseCase? permissions = null,
        IListAuthorizationRolesUseCase? roles = null,
        IReplaceUserRolesUseCase? replaceUserRoles = null)
    {
        return new AuthorizationController(
            permissions ?? Mock.Of<IListPermissionsUseCase>(),
            roles ?? Mock.Of<IListAuthorizationRolesUseCase>(),
            Mock.Of<IGetAuthorizationRoleUseCase>(),
            Mock.Of<ICreateAuthorizationRoleUseCase>(),
            Mock.Of<IUpdateAuthorizationRoleUseCase>(),
            Mock.Of<IReplaceRolePermissionsUseCase>(),
            Mock.Of<IGetUserAuthorizationUseCase>(),
            replaceUserRoles ?? Mock.Of<IReplaceUserRolesUseCase>(),
            Mock.Of<IListAuthorizationAuditUseCase>(),
            TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
