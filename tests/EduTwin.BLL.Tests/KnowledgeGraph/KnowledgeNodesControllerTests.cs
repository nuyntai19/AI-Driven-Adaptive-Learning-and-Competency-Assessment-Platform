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
    private readonly Mock<IListKnowledgeNodesUseCase> _listUseCaseMock;
    private readonly Mock<ICreateKnowledgeNodeUseCase> _createUseCaseMock;
    private readonly Mock<IUpdateKnowledgeNodeUseCase> _updateUseCaseMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly KnowledgeNodesController _sut;

    public KnowledgeNodesControllerTests()
    {
        _listUseCaseMock = new Mock<IListKnowledgeNodesUseCase>();
        _createUseCaseMock = new Mock<ICreateKnowledgeNodeUseCase>();
        _updateUseCaseMock = new Mock<IUpdateKnowledgeNodeUseCase>();
        _timeProviderMock = new Mock<TimeProvider>();

        var utcNow = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);

        _sut = new KnowledgeNodesController(_listUseCaseMock.Object, _createUseCaseMock.Object, _updateUseCaseMock.Object, _timeProviderMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/knowledge/nodes/1";
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

        _listUseCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
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

        _listUseCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
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

        _listUseCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
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

        _listUseCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
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

        _listUseCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
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
        _listUseCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .Callback<KnowledgeNodeListQuery, CancellationToken>((q, t) => passedToken = t)
            .ReturnsAsync(ListKnowledgeNodesResult.Success(new List<KnowledgeNodeDto>()));

        await _sut.ListKnowledgeNodes(query, token);

        Assert.Equal(token, passedToken);
    }
    // ── CreateKnowledgeNode tests ──

    [Fact]
    public async Task CreateKnowledgeNode_Success_Returns201WithDataAndMeta()
    {
        var request = new CreateKnowledgeNodeRequest { SubjectId = Guid.NewGuid() };
        var expectedData = new KnowledgeNodeDto { NodeId = "1", NodeName = "Test" };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeNodeResult.Success(expectedData));

        var result = await _sut.CreateKnowledgeNode(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<KnowledgeNodeResponse>(createdResult.Value);

        Assert.Equal(string.Empty, createdResult.Location);
        Assert.Same(expectedData, response.Data);
        Assert.NotNull(response.Meta);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, response.Meta.Timestamp);
    }

    [Fact]
    public async Task CreateKnowledgeNode_ValidationFailed_Returns400()
    {
        var request = new CreateKnowledgeNodeRequest();

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.CreateKnowledgeNode(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1", problemDetails.Type);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateKnowledgeNode_ResourceNotFound_Returns404()
    {
        var request = new CreateKnowledgeNodeRequest { SubjectId = Guid.NewGuid() };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.CreateKnowledgeNode(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4", problemDetails.Type);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateKnowledgeNode_DuplicateResource_Returns409()
    {
        var request = new CreateKnowledgeNodeRequest { SubjectId = Guid.NewGuid() };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeNodeResult.Failure(ErrorCodes.DuplicateResource));

        var result = await _sut.CreateKnowledgeNode(request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);

        Assert.Equal(409, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8", problemDetails.Type);
        Assert.Equal(ErrorCodes.DuplicateResource, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateKnowledgeNode_UnexpectedErrorCode_ThrowsInvalidOperationException()
    {
        var request = new CreateKnowledgeNodeRequest { SubjectId = Guid.NewGuid() };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeNodeResult.Failure("UNKNOWN_ERROR"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateKnowledgeNode(request, CancellationToken.None));

        Assert.Contains("UNKNOWN_ERROR", ex.Message);
    }

    [Fact]
    public async Task CreateKnowledgeNode_ExactCancellationToken_IsPassed()
    {
        var request = new CreateKnowledgeNodeRequest { SubjectId = Guid.NewGuid() };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        CancellationToken? passedToken = null;
        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .Callback<CreateKnowledgeNodeRequest, CancellationToken>((q, t) => passedToken = t)
            .ReturnsAsync(CreateKnowledgeNodeResult.Success(new KnowledgeNodeDto()));

        await _sut.CreateKnowledgeNode(request, token);

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

    [Fact]
    public void CreateKnowledgeNode_HasHttpPostAttribute()
    {
        var method = typeof(KnowledgeNodesController)
            .GetMethod(nameof(KnowledgeNodesController.CreateKnowledgeNode));

        Assert.NotNull(method);
        var httpPostAttr = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(httpPostAttr);
    }

    [Fact]
    public void CreateKnowledgeNode_HasActionLevelAuthorizeWithTeacherOrCenterManagerPolicy()
    {
        var method = typeof(KnowledgeNodesController)
            .GetMethod(nameof(KnowledgeNodesController.CreateKnowledgeNode));

        Assert.NotNull(method);

        var authorizeAttrs = method!.GetCustomAttributes<AuthorizeAttribute>().ToList();
        Assert.Single(authorizeAttrs);

        var attr = authorizeAttrs[0];
        Assert.Equal(EduTwin.BLL.IdentityAndTenancy.AuthorizationPolicies.TeacherOrCenterManager, attr.Policy);
    }

    // ─── UpdateKnowledgeNode tests ───

    [Fact]
    public async Task UpdateKnowledgeNode_Success_Returns200KnowledgeNodeResponse()
    {
        var request = new UpdateKnowledgeNodeRequest { NodeName = "Updated" };
        var expectedData = new KnowledgeNodeDto { NodeId = "1", NodeName = "Updated" };

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Success(expectedData));

        var result = await _sut.UpdateKnowledgeNode("1", request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeNodeResponse>(okResult.Value);

        Assert.Same(expectedData, response.Data);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_Success_MetaContainsExactTraceIdAndTimestamp()
    {
        var request = new UpdateKnowledgeNodeRequest { NodeName = "Updated" };
        var expectedData = new KnowledgeNodeDto { NodeId = "1", NodeName = "Updated" };

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Success(expectedData));

        var result = await _sut.UpdateKnowledgeNode("1", request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeNodeResponse>(okResult.Value);

        Assert.NotNull(response.Meta);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, response.Meta.Timestamp);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_PassesExactRawNodeId()
    {
        var rawNodeId = " 1";
        var request = new UpdateKnowledgeNodeRequest { NodeName = "Updated" };
        var expectedData = new KnowledgeNodeDto { NodeId = "1", NodeName = "Updated" };

        _updateUseCaseMock.Setup(x => x.ExecuteAsync(rawNodeId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Success(expectedData))
            .Verifiable();

        await _sut.UpdateKnowledgeNode(rawNodeId, request, CancellationToken.None);

        _updateUseCaseMock.Verify(x => x.ExecuteAsync(rawNodeId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_PassesSameRequestInstance()
    {
        var request = new UpdateKnowledgeNodeRequest { NodeName = "Updated" };

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Success(new KnowledgeNodeDto()))
            .Verifiable();

        await _sut.UpdateKnowledgeNode("1", request, CancellationToken.None);

        _updateUseCaseMock.Verify(x => x.ExecuteAsync("1", It.Is<UpdateKnowledgeNodeRequest>(r => ReferenceEquals(r, request)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_PassesExactCancellationToken()
    {
        var request = new UpdateKnowledgeNodeRequest { NodeName = "Updated" };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        CancellationToken? passedToken = null;
        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .Callback<string, UpdateKnowledgeNodeRequest, CancellationToken>((id, r, t) => passedToken = t)
            .ReturnsAsync(UpdateKnowledgeNodeResult.Success(new KnowledgeNodeDto()));

        await _sut.UpdateKnowledgeNode("1", request, token);

        Assert.Equal(token, passedToken);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_ValidationFailed_Returns400ProblemDetails()
    {
        var request = new UpdateKnowledgeNodeRequest();

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.UpdateKnowledgeNode("1", request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal(400, problemDetails.Status);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal("/api/v1/knowledge/nodes/1", problemDetails.Instance);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_ResourceNotFound_Returns404ProblemDetails()
    {
        var request = new UpdateKnowledgeNodeRequest();

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.UpdateKnowledgeNode("1", request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(404, problemDetails.Status);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal("/api/v1/knowledge/nodes/1", problemDetails.Instance);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_ConcurrencyConflict_Returns409ProblemDetails()
    {
        var request = new UpdateKnowledgeNodeRequest();

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Failure(ErrorCodes.ConcurrencyConflict));

        var result = await _sut.UpdateKnowledgeNode("1", request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);

        Assert.Equal(409, problemDetails.Status);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal("/api/v1/knowledge/nodes/1", problemDetails.Instance);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_DagCycleDetected_Returns409ProblemDetails()
    {
        var request = new UpdateKnowledgeNodeRequest();

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Failure(ErrorCodes.DagCycleDetected));

        var result = await _sut.UpdateKnowledgeNode("1", request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);

        Assert.Equal(409, problemDetails.Status);
        Assert.Equal(ErrorCodes.DagCycleDetected, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal("/api/v1/knowledge/nodes/1", problemDetails.Instance);
    }

    [Fact]
    public async Task UpdateKnowledgeNode_UnexpectedErrorCode_ThrowsInvalidOperationException()
    {
        var request = new UpdateKnowledgeNodeRequest();

        _updateUseCaseMock.Setup(x => x.ExecuteAsync("1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateKnowledgeNodeResult.Failure("UNKNOWN_ERROR"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateKnowledgeNode("1", request, CancellationToken.None));

        Assert.Contains("UNKNOWN_ERROR", ex.Message);
    }

    [Fact]
    public void UpdateKnowledgeNode_HasHttpPatchWithExactTemplate()
    {
        var method = typeof(KnowledgeNodesController)
            .GetMethod(nameof(KnowledgeNodesController.UpdateKnowledgeNode));

        Assert.NotNull(method);
        var httpAttr = method!.GetCustomAttribute<HttpPatchAttribute>();
        Assert.NotNull(httpAttr);
        Assert.Equal("{nodeId}", httpAttr.Template);
    }

    [Fact]
    public void UpdateKnowledgeNode_HasTeacherOrCenterManagerPolicy()
    {
        var method = typeof(KnowledgeNodesController)
            .GetMethod(nameof(KnowledgeNodesController.UpdateKnowledgeNode));

        Assert.NotNull(method);

        var authorizeAttrs = method!.GetCustomAttributes<AuthorizeAttribute>().ToList();
        Assert.Single(authorizeAttrs);

        var attr = authorizeAttrs[0];
        Assert.Equal(EduTwin.BLL.IdentityAndTenancy.AuthorizationPolicies.TeacherOrCenterManager, attr.Policy);
    }

    [Fact]
    public void UpdateKnowledgeNode_DeclaresExpectedProducesResponseTypes()
    {
        var method = typeof(KnowledgeNodesController)
            .GetMethod(nameof(KnowledgeNodesController.UpdateKnowledgeNode));

        Assert.NotNull(method);

        var producesAttrs = method!.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();

        Assert.Contains(producesAttrs, a => a.Type == typeof(KnowledgeNodeResponse) && a.StatusCode == StatusCodes.Status200OK);
        Assert.Contains(producesAttrs, a => a.Type == typeof(ProblemDetails) && a.StatusCode == StatusCodes.Status400BadRequest);
        Assert.Contains(producesAttrs, a => a.Type == typeof(ProblemDetails) && a.StatusCode == StatusCodes.Status404NotFound);
        Assert.Contains(producesAttrs, a => a.Type == typeof(ProblemDetails) && a.StatusCode == StatusCodes.Status409Conflict);
    }

}
