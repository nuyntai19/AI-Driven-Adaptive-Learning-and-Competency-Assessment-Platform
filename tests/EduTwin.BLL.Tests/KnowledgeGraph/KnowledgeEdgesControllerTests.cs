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

public class KnowledgeEdgesControllerTests
{
    private readonly Mock<ICreateKnowledgeEdgeUseCase> _createUseCaseMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly KnowledgeEdgesController _sut;

    public KnowledgeEdgesControllerTests()
    {
        _createUseCaseMock = new Mock<ICreateKnowledgeEdgeUseCase>();
        _timeProviderMock = new Mock<TimeProvider>();

        var utcNow = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);

        _sut = new KnowledgeEdgesController(_createUseCaseMock.Object, _timeProviderMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/knowledge/edges";
        httpContext.TraceIdentifier = "test-trace-id";
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task CreateKnowledgeEdge_Success_Returns201WithExactData()
    {
        var subjectId = Guid.NewGuid();
        var request = new CreateKnowledgeEdgeRequest
        {
            SubjectId = subjectId,
            SourceNodeId = "1",
            TargetNodeId = "2",
            RelationType = "PrerequisiteOf"
        };
        var expectedDto = new KnowledgeEdgeDto
        {
            EdgeId = "10",
            SubjectId = subjectId.ToString("D"),
            SourceNodeId = "1",
            TargetNodeId = "2",
            RelationType = "PrerequisiteOf",
            Weight = 1.0m,
            RowVersion = "1"
        };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeEdgeResult.Success(expectedDto));

        var result = await _sut.CreateKnowledgeEdge(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<KnowledgeEdgeResponse>(createdResult.Value);

        Assert.Same(expectedDto, response.Data);
    }

    [Fact]
    public async Task CreateKnowledgeEdge_Success_MetaContainsExactTraceIdAndTimestamp()
    {
        var request = new CreateKnowledgeEdgeRequest
        {
            SubjectId = Guid.NewGuid(),
            SourceNodeId = "1",
            TargetNodeId = "2"
        };
        var expectedDto = new KnowledgeEdgeDto { EdgeId = "10" };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeEdgeResult.Success(expectedDto));

        var result = await _sut.CreateKnowledgeEdge(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<KnowledgeEdgeResponse>(createdResult.Value);

        Assert.NotNull(response.Meta);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
        Assert.Equal(new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.Zero).UtcDateTime, response.Meta.Timestamp);
    }

    [Fact]
    public async Task CreateKnowledgeEdge_ValidationFailed_Returns400()
    {
        var request = new CreateKnowledgeEdgeRequest();

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeEdgeResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.CreateKnowledgeEdge(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1", problemDetails.Type);
        Assert.Equal("Dữ liệu không hợp lệ", problemDetails.Title);
        Assert.Equal("Dữ liệu gửi lên không đúng định dạng hoặc thiếu thông tin.", problemDetails.Detail);
        Assert.Equal("/api/v1/knowledge/edges", problemDetails.Instance);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateKnowledgeEdge_ResourceNotFound_Returns404()
    {
        var request = new CreateKnowledgeEdgeRequest
        {
            SubjectId = Guid.NewGuid(),
            SourceNodeId = "1",
            TargetNodeId = "2"
        };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeEdgeResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.CreateKnowledgeEdge(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4", problemDetails.Type);
        Assert.Equal("Không tìm thấy dữ liệu", problemDetails.Title);
        Assert.Equal("Dữ liệu liên quan không tồn tại hoặc bạn không có quyền truy cập.", problemDetails.Detail);
        Assert.Equal("/api/v1/knowledge/edges", problemDetails.Instance);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateKnowledgeEdge_DuplicateResource_Returns409()
    {
        var request = new CreateKnowledgeEdgeRequest
        {
            SubjectId = Guid.NewGuid(),
            SourceNodeId = "1",
            TargetNodeId = "2"
        };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeEdgeResult.Failure(ErrorCodes.DuplicateResource));

        var result = await _sut.CreateKnowledgeEdge(request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);

        Assert.Equal(409, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8", problemDetails.Type);
        Assert.Equal("Trùng lặp dữ liệu", problemDetails.Title);
        Assert.Equal("Quan hệ giữa hai node đã tồn tại trong môn học.", problemDetails.Detail);
        Assert.Equal("/api/v1/knowledge/edges", problemDetails.Instance);
        Assert.Equal(ErrorCodes.DuplicateResource, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateKnowledgeEdge_DagCycleDetected_Returns409()
    {
        var request = new CreateKnowledgeEdgeRequest
        {
            SubjectId = Guid.NewGuid(),
            SourceNodeId = "1",
            TargetNodeId = "2"
        };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeEdgeResult.Failure(ErrorCodes.DagCycleDetected));

        var result = await _sut.CreateKnowledgeEdge(request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);

        Assert.Equal(409, problemDetails.Status);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8", problemDetails.Type);
        Assert.Equal("Phát hiện chu trình trong đồ thị", problemDetails.Title);
        Assert.Equal("Tạo quan hệ này sẽ dẫn đến chu trình phụ thuộc không hợp lệ.", problemDetails.Detail);
        Assert.Equal("/api/v1/knowledge/edges", problemDetails.Instance);
        Assert.Equal(ErrorCodes.DagCycleDetected, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateKnowledgeEdge_UnexpectedErrorCode_ThrowsInvalidOperationException()
    {
        var request = new CreateKnowledgeEdgeRequest();

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateKnowledgeEdgeResult.Failure("UNKNOWN_ERROR"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CreateKnowledgeEdge(request, CancellationToken.None));

        Assert.Contains("UNKNOWN_ERROR", ex.Message);
    }

    [Fact]
    public async Task CreateKnowledgeEdge_PassesExactCancellationToken()
    {
        var request = new CreateKnowledgeEdgeRequest();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        CancellationToken? passedToken = null;
        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .Callback<CreateKnowledgeEdgeRequest, CancellationToken>((r, t) => passedToken = t)
            .ReturnsAsync(CreateKnowledgeEdgeResult.Success(new KnowledgeEdgeDto()));

        await _sut.CreateKnowledgeEdge(request, token);

        Assert.Equal(token, passedToken);
    }

    [Fact]
    public void Controller_RouteAttribute_IsExactApiV1KnowledgeEdges()
    {
        var routeAttr = typeof(KnowledgeEdgesController).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/knowledge/edges", routeAttr!.Template);
    }

    [Fact]
    public void Action_HasHttpPostAttribute()
    {
        var method = typeof(KnowledgeEdgesController)
            .GetMethod(nameof(KnowledgeEdgesController.CreateKnowledgeEdge));

        Assert.NotNull(method);
        var httpAttr = method!.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(httpAttr);
    }

    [Fact]
    public void Action_HasTeacherOrCenterManagerPolicy()
    {
        var method = typeof(KnowledgeEdgesController)
            .GetMethod(nameof(KnowledgeEdgesController.CreateKnowledgeEdge));

        Assert.NotNull(method);

        var authorizeAttrs = method!.GetCustomAttributes<AuthorizeAttribute>().ToList();
        Assert.Single(authorizeAttrs);

        var attr = authorizeAttrs[0];
        Assert.Equal(EduTwin.BLL.IdentityAndTenancy.AuthorizationPolicies.TeacherOrCenterManager, attr.Policy);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var authAttr = typeof(KnowledgeEdgesController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authAttr);
    }

    [Fact]
    public void Action_ProducesResponseType_MetadataExactFor201400404409()
    {
        var method = typeof(KnowledgeEdgesController)
            .GetMethod(nameof(KnowledgeEdgesController.CreateKnowledgeEdge));

        Assert.NotNull(method);

        var producesAttrs = method!.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();

        Assert.Contains(producesAttrs, a => a.Type == typeof(KnowledgeEdgeResponse) && a.StatusCode == StatusCodes.Status201Created);
        Assert.Contains(producesAttrs, a => a.Type == typeof(ProblemDetails) && a.StatusCode == StatusCodes.Status400BadRequest);
        Assert.Contains(producesAttrs, a => a.Type == typeof(ProblemDetails) && a.StatusCode == StatusCodes.Status404NotFound);
        Assert.Contains(producesAttrs, a => a.Type == typeof(ProblemDetails) && a.StatusCode == StatusCodes.Status409Conflict);
    }
}
