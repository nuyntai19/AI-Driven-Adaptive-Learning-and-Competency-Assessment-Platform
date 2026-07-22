using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using EduTwin.API.Controllers;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class KnowledgeNodesControllerTests
{
    private readonly Mock<IListKnowledgeNodesUseCase> _useCaseMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly KnowledgeNodesController _sut;

    public KnowledgeNodesControllerTests()
    {
        _useCaseMock = new Mock<IListKnowledgeNodesUseCase>();
        _timeProviderMock = new Mock<TimeProvider>();

        var utcNow = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);

        _sut = new KnowledgeNodesController(_useCaseMock.Object, _timeProviderMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ListKnowledgeNodes_Success_Returns200WithDataAndMeta()
    {
        var query = new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() };
        var expectedData = new List<KnowledgeNodeDto>
        {
            new KnowledgeNodeDto { NodeId = "1", NodeName = "Test" }
        };

        _useCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListKnowledgeNodesResult.Success(expectedData));

        var result = await _sut.ListKnowledgeNodes(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeNodeListResponse>(okResult.Value);

        Assert.Same(expectedData, response.Data);
        Assert.NotNull(response.Meta);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, response.Meta.Timestamp);
    }

    [Fact]
    public async Task ListKnowledgeNodes_EmptyCollection_Returns200WithEmptyData()
    {
        var query = new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() };

        _useCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListKnowledgeNodesResult.Success(new List<KnowledgeNodeDto>()));

        var result = await _sut.ListKnowledgeNodes(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeNodeListResponse>(okResult.Value);

        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task ListKnowledgeNodes_ValidationFailed_Returns400()
    {
        var query = new KnowledgeNodeListQuery();

        _useCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListKnowledgeNodesResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.ListKnowledgeNodes(query, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1", problemDetails.Type);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task ListKnowledgeNodes_ResourceNotFound_Returns404()
    {
        var query = new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() };

        _useCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListKnowledgeNodesResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.ListKnowledgeNodes(query, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4", problemDetails.Type);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task ListKnowledgeNodes_UnexpectedErrorCode_ThrowsInvalidOperationException()
    {
        var query = new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() };

        _useCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListKnowledgeNodesResult.Failure("UNKNOWN_ERROR"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ListKnowledgeNodes(query, CancellationToken.None));

        Assert.Contains("UNKNOWN_ERROR", ex.Message);
    }

    [Fact]
    public async Task ListKnowledgeNodes_ExactCancellationToken_IsPassed()
    {
        var query = new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        CancellationToken? passedToken = null;
        _useCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .Callback<KnowledgeNodeListQuery, CancellationToken>((q, t) => passedToken = t)
            .ReturnsAsync(ListKnowledgeNodesResult.Success(new List<KnowledgeNodeDto>()));

        await _sut.ListKnowledgeNodes(query, token);

        Assert.Equal(token, passedToken);
    }

    // ── Controller metadata reflection tests ──

    [Fact]
    public void Controller_HasRouteAttribute_WithExactTemplate()
    {
        var routeAttr = typeof(KnowledgeNodesController)
            .GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/knowledge/nodes", routeAttr!.Template);
    }

    [Fact]
    public void ListKnowledgeNodes_HasHttpGetAttribute()
    {
        var method = typeof(KnowledgeNodesController)
            .GetMethod(nameof(KnowledgeNodesController.ListKnowledgeNodes));

        Assert.NotNull(method);
        var httpGetAttr = method!.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(httpGetAttr);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute_WithoutRoleOrPolicy()
    {
        var authorizeAttrs = typeof(KnowledgeNodesController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .ToList();

        Assert.Single(authorizeAttrs);

        var attr = authorizeAttrs[0];
        Assert.Null(attr.Roles);
        Assert.Null(attr.Policy);
    }

    [Fact]
    public void ListKnowledgeNodes_HasNoActionLevelAuthorizeWithRoleOrPolicy()
    {
        var method = typeof(KnowledgeNodesController)
            .GetMethod(nameof(KnowledgeNodesController.ListKnowledgeNodes));

        Assert.NotNull(method);

        var actionAuthorizeAttrs = method!.GetCustomAttributes<AuthorizeAttribute>().ToList();

        // Either no action-level Authorize, or if present, it must not restrict by role/policy
        foreach (var attr in actionAuthorizeAttrs)
        {
            Assert.Null(attr.Roles);
            Assert.Null(attr.Policy);
        }
    }
}
