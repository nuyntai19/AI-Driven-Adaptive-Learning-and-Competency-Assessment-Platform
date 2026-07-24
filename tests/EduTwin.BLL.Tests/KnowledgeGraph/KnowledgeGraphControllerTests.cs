using System;
using System.Diagnostics;
using System.IO;
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

public class KnowledgeGraphControllerTests
{
    private readonly Mock<IGetKnowledgeGraphUseCase> _useCaseMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly KnowledgeGraphController _sut;
    private readonly DateTimeOffset _fixedUtcNow;

    public KnowledgeGraphControllerTests()
    {
        _useCaseMock = new Mock<IGetKnowledgeGraphUseCase>();
        _timeProviderMock = new Mock<TimeProvider>();

        _fixedUtcNow = new DateTimeOffset(2026, 7, 24, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(_fixedUtcNow);

        _sut = new KnowledgeGraphController(_useCaseMock.Object, _timeProviderMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/knowledge/graph";
        httpContext.TraceIdentifier = "http-fallback-trace-id";

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public void Controller_HasApiControllerAttribute()
    {
        var attr = typeof(KnowledgeGraphController).GetCustomAttribute<ApiControllerAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void Controller_HasExactRouteAttribute()
    {
        var attr = typeof(KnowledgeGraphController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("api/v1/knowledge/graph", attr.Template);
    }

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var attr = typeof(KnowledgeGraphController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void Controller_HasNoRoleSpecificPolicy()
    {
        var attr = typeof(KnowledgeGraphController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Null(attr.Policy);
        Assert.Null(attr.Roles);
    }

    [Fact]
    public void GetKnowledgeGraph_HasHttpGetAttribute()
    {
        var method = typeof(KnowledgeGraphController).GetMethod(nameof(KnowledgeGraphController.GetKnowledgeGraph));
        Assert.NotNull(method);

        var attr = method!.GetCustomAttribute<HttpGetAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void GetKnowledgeGraph_DeclaresExactThreeProducesResponseTypeAttributes()
    {
        var method = typeof(KnowledgeGraphController).GetMethod(nameof(KnowledgeGraphController.GetKnowledgeGraph));
        Assert.NotNull(method);

        var attrs = method!.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();
        Assert.Equal(3, attrs.Count);

        var okAttr = attrs.SingleOrDefault(a => a.StatusCode == StatusCodes.Status200OK);
        Assert.NotNull(okAttr);
        Assert.Equal(typeof(KnowledgeGraphResponse), okAttr!.Type);

        var badRequestAttr = attrs.SingleOrDefault(a => a.StatusCode == StatusCodes.Status400BadRequest);
        Assert.NotNull(badRequestAttr);
        Assert.Equal(typeof(ProblemDetails), badRequestAttr!.Type);

        var notFoundAttr = attrs.SingleOrDefault(a => a.StatusCode == StatusCodes.Status404NotFound);
        Assert.NotNull(notFoundAttr);
        Assert.Equal(typeof(ProblemDetails), notFoundAttr!.Type);
    }

    [Fact]
    public async Task GetKnowledgeGraph_Success_ReturnsOkObjectResult()
    {
        var subjectId = Guid.NewGuid();
        var graphDto = new KnowledgeGraphDto { SubjectId = subjectId.ToString("D") };

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(graphDto));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Fact]
    public async Task GetKnowledgeGraph_Success_BodyIsKnowledgeGraphResponse()
    {
        var subjectId = Guid.NewGuid();
        var graphDto = new KnowledgeGraphDto { SubjectId = subjectId.ToString("D") };

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(graphDto));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<KnowledgeGraphResponse>(okResult.Value);
    }

    [Fact]
    public async Task GetKnowledgeGraph_Success_PreservesExactDataDtoFromBLL()
    {
        var subjectId = Guid.NewGuid();
        var graphDto = new KnowledgeGraphDto
        {
            SubjectId = subjectId.ToString("D"),
            Nodes = new[] { new KnowledgeGraphNodeDto { NodeId = "101", NodeCode = "CODE1" } },
            Edges = new[] { new KnowledgeGraphEdgeDto { EdgeId = "501", SourceNodeId = "101", TargetNodeId = "102" } }
        };

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(graphDto));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeGraphResponse>(okResult.Value);

        Assert.Same(graphDto, response.Data);
    }

    [Fact]
    public async Task GetKnowledgeGraph_Success_MetaContainsTraceIdFromActivityCurrentWhenPresent()
    {
        var subjectId = Guid.NewGuid();
        var graphDto = new KnowledgeGraphDto { SubjectId = subjectId.ToString("D") };

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(graphDto));

        using var activity = new Activity("TestActivity").Start();

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeGraphResponse>(okResult.Value);

        Assert.NotNull(response.Meta);
        Assert.Equal(activity.Id, response.Meta.TraceId);
    }

    [Fact]
    public async Task GetKnowledgeGraph_Success_MetaFallbacksToHttpContextTraceIdentifierWhenNoActivity()
    {
        var subjectId = Guid.NewGuid();
        var graphDto = new KnowledgeGraphDto { SubjectId = subjectId.ToString("D") };

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(graphDto));

        Activity.Current = null;

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeGraphResponse>(okResult.Value);

        Assert.NotNull(response.Meta);
        Assert.Equal("http-fallback-trace-id", response.Meta.TraceId);
    }

    [Fact]
    public async Task GetKnowledgeGraph_Success_MetaTimestampFromInjectedTimeProvider()
    {
        var subjectId = Guid.NewGuid();
        var graphDto = new KnowledgeGraphDto { SubjectId = subjectId.ToString("D") };

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(graphDto));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<KnowledgeGraphResponse>(okResult.Value);

        Assert.NotNull(response.Meta);
        Assert.Equal(_fixedUtcNow.UtcDateTime, response.Meta.Timestamp);
    }

    [Fact]
    public async Task GetKnowledgeGraph_PassesExactSubjectIdToUseCase()
    {
        var subjectId = Guid.NewGuid();
        var graphDto = new KnowledgeGraphDto { SubjectId = subjectId.ToString("D") };

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(graphDto));

        await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        _useCaseMock.Verify(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetKnowledgeGraph_PassesExactCancellationTokenToUseCase()
    {
        var subjectId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, token))
            .ReturnsAsync(GetKnowledgeGraphResult.Success(new KnowledgeGraphDto()));

        await _sut.GetKnowledgeGraph(subjectId, token);

        _useCaseMock.Verify(x => x.ExecuteAsync(subjectId, token), Times.Once);
    }

    [Fact]
    public async Task GetKnowledgeGraph_ValidationFailed_Returns400BadRequest()
    {
        var subjectId = Guid.Empty;

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task GetKnowledgeGraph_ValidationFailed_ProblemDetailsHasRequiredFields()
    {
        var subjectId = Guid.Empty;

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1", problemDetails.Type);
        Assert.Equal("Dữ liệu không hợp lệ", problemDetails.Title);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Dữ liệu gửi lên không đúng định dạng hoặc thiếu thông tin.", problemDetails.Detail);
        Assert.Equal("/api/v1/knowledge/graph", problemDetails.Instance);
    }

    [Fact]
    public async Task GetKnowledgeGraph_ValidationFailed_ProblemDetailsExtensionsHasExactTraceIdAndErrorCode()
    {
        var subjectId = Guid.Empty;

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal("http-fallback-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
    }

    [Fact]
    public async Task GetKnowledgeGraph_ResourceNotFound_Returns404NotFound()
    {
        var subjectId = Guid.NewGuid();

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task GetKnowledgeGraph_ResourceNotFound_ProblemDetailsHasRequiredFields()
    {
        var subjectId = Guid.NewGuid();

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4", problemDetails.Type);
        Assert.Equal("Không tìm thấy dữ liệu", problemDetails.Title);
        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Dữ liệu liên quan không tồn tại hoặc bạn không có quyền truy cập.", problemDetails.Detail);
        Assert.Equal("/api/v1/knowledge/graph", problemDetails.Instance);
    }

    [Fact]
    public async Task GetKnowledgeGraph_ResourceNotFound_ProblemDetailsExtensionsHasExactTraceIdAndErrorCode()
    {
        var subjectId = Guid.NewGuid();

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.GetKnowledgeGraph(subjectId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal("http-fallback-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
    }

    [Fact]
    public async Task GetKnowledgeGraph_UnknownErrorCode_ThrowsInvalidOperationException()
    {
        var subjectId = Guid.NewGuid();

        _useCaseMock.Setup(x => x.ExecuteAsync(subjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetKnowledgeGraphResult.Failure("UNKNOWN_ERROR_CODE"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetKnowledgeGraph(subjectId, CancellationToken.None));
        Assert.Contains("UNKNOWN_ERROR_CODE", ex.Message);
    }

    [Fact]
    public void Controller_Constructor_DoesNotDependOnDbContext()
    {
        var constructors = typeof(KnowledgeGraphController).GetConstructors();
        foreach (var ctor in constructors)
        {
            var paramTypes = ctor.GetParameters().Select(p => p.ParameterType);
            Assert.DoesNotContain(paramTypes, t => t.Name.Contains("DbContext"));
        }
    }

    [Fact]
    public void Controller_DoesNotPerformDirectDataAccess()
    {
        var fields = typeof(KnowledgeGraphController).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            Assert.False(field.FieldType.Name.Contains("DbContext"), $"Field '{field.Name}' references DbContext directly.");
        }
    }

    [Fact]
    public void Controller_Source_Contains_No_IgnoreQueryFilters()
    {
        var controllerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.API", "Controllers", "KnowledgeGraphController.cs");
        var fullPath = Path.GetFullPath(controllerPath);

        Assert.True(File.Exists(fullPath), $"Controller file does not exist at expected path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("IgnoreQueryFilters", content);
    }

    [Fact]
    public void KnowledgeGraphResponseWrapper_HasOnlyDataAndMetaProperties()
    {
        var properties = typeof(KnowledgeGraphResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).OrderBy(x => x).ToList();

        var expectedProperties = new[] { "Data", "Meta" }.OrderBy(x => x).ToList();

        Assert.Equal(expectedProperties, properties);
    }
}
