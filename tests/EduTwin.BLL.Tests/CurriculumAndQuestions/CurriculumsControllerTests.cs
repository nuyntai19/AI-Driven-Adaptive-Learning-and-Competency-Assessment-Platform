using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using EduTwin.API.Controllers;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class CurriculumsControllerTests
{
    private readonly Mock<ICreateCurriculumUseCase> _createUseCaseMock;
    private readonly Mock<IListCurriculumsUseCase> _listUseCaseMock;
    private readonly Mock<IGetCurriculumUseCase> _getUseCaseMock;
    private readonly Mock<IUpdateCurriculumUseCase> _updateUseCaseMock;
    private readonly Mock<IAssignCurriculumClassesUseCase> _assignClassesUseCaseMock;
    private readonly Mock<IAssignCurriculumNodesUseCase> _assignNodesUseCaseMock;
    private readonly Mock<IPublishCurriculumUseCase> _publishUseCaseMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CurriculumsController _sut;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 7, 24, 14, 0, 0, TimeSpan.Zero);

    public CurriculumsControllerTests()
    {
        _createUseCaseMock = new Mock<ICreateCurriculumUseCase>();
        _listUseCaseMock = new Mock<IListCurriculumsUseCase>();
        _getUseCaseMock = new Mock<IGetCurriculumUseCase>();
        _updateUseCaseMock = new Mock<IUpdateCurriculumUseCase>();
        _assignClassesUseCaseMock = new Mock<IAssignCurriculumClassesUseCase>();
        _assignNodesUseCaseMock = new Mock<IAssignCurriculumNodesUseCase>();
        _publishUseCaseMock = new Mock<IPublishCurriculumUseCase>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _sut = new CurriculumsController(
            _createUseCaseMock.Object,
            _listUseCaseMock.Object,
            _getUseCaseMock.Object,
            _updateUseCaseMock.Object,
            _assignClassesUseCaseMock.Object,
            _assignNodesUseCaseMock.Object,
            _publishUseCaseMock.Object,
            _timeProviderMock.Object);

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        httpContext.Request.Path = "/api/v1/curriculums";

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task CreateCurriculum_Success_Returns201Envelope()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Toán 12 Cơ Bản",
            NodeIds = new List<string> { "1" }
        };
        var expectedDto = new CurriculumDto
        {
            CurriculumId = Guid.NewGuid().ToString("D"),
            TeacherId = Guid.NewGuid().ToString("D"),
            SubjectId = request.SubjectId.ToString("D"),
            Title = request.Title,
            ReviewStatus = "Draft",
            NodeIds = request.NodeIds,
            RowVersion = "1"
        };

        _createUseCaseMock
            .Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCurriculumResult.Success(expectedDto));

        var result = await _sut.CreateCurriculum(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<CurriculumResponse>(createdResult.Value);

        Assert.Same(expectedDto, response.Data);
        Assert.NotNull(response.Meta);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
        Assert.Equal(_fixedTime.UtcDateTime, response.Meta.Timestamp);
    }

    [Fact]
    public async Task CreateCurriculum_PassesExactRequestAndCancellationToken()
    {
        var request = new CreateCurriculumRequest
        {
            SubjectId = Guid.NewGuid(),
            Title = "Thí nghiệm CancellationToken",
            NodeIds = new List<string>()
        };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var dto = new CurriculumDto { CurriculumId = Guid.NewGuid().ToString("D") };

        _createUseCaseMock
            .Setup(x => x.ExecuteAsync(It.IsAny<CreateCurriculumRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCurriculumResult.Success(dto));

        await _sut.CreateCurriculum(request, token);

        _createUseCaseMock.Verify(x => x.ExecuteAsync(request, token), Times.Once);
    }

    [Fact]
    public async Task CreateCurriculum_ValidationFailed_Returns400ProblemDetails()
    {
        var request = new CreateCurriculumRequest();

        _createUseCaseMock
            .Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCurriculumResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.CreateCurriculum(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Dữ liệu không hợp lệ", problemDetails.Title);
        Assert.Equal("/api/v1/curriculums", problemDetails.Instance);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateCurriculum_ResourceNotFound_Returns404ProblemDetails()
    {
        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Unknown Subject" };

        _createUseCaseMock
            .Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCurriculumResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.CreateCurriculum(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Không tìm thấy dữ liệu", problemDetails.Title);
        Assert.Equal("/api/v1/curriculums", problemDetails.Instance);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task CreateCurriculum_UnexpectedErrorCode_ThrowsInvalidOperationException()
    {
        var request = new CreateCurriculumRequest { SubjectId = Guid.NewGuid(), Title = "Unexpected Error" };

        _createUseCaseMock
            .Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCurriculumResult.Failure("UNEXPECTED_ERROR_CODE"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateCurriculum(request, CancellationToken.None));

        Assert.Contains("UNEXPECTED_ERROR_CODE", ex.Message);
    }

    [Fact]
    public void CurriculumsController_MetadataAndAttributes_AreCorrect()
    {
        var controllerType = typeof(CurriculumsController);

        // Controller level attributes
        Assert.NotNull(controllerType.GetCustomAttribute<ApiControllerAttribute>());

        var routeAttr = controllerType.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/curriculums", routeAttr.Template);

        Assert.NotNull(controllerType.GetCustomAttribute<AuthorizeAttribute>());

        // CreateCurriculum Method level attributes
        var createMethod = controllerType.GetMethod(nameof(CurriculumsController.CreateCurriculum));
        Assert.NotNull(createMethod);

        Assert.NotNull(createMethod.GetCustomAttribute<HttpPostAttribute>());

        var createAuthAttr = createMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAuthAttr);
        Assert.Equal("curriculum.curriculums.create", createAuthAttr.Policy);

        var createProducesAttrs = createMethod.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();
        Assert.Equal(3, createProducesAttrs.Count);
        Assert.NotNull(createProducesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status201Created && a.Type == typeof(CurriculumResponse)));
        Assert.NotNull(createProducesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status400BadRequest && a.Type == typeof(ProblemDetails)));
        Assert.NotNull(createProducesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status404NotFound && a.Type == typeof(ProblemDetails)));

        // ListCurriculums Method level attributes
        var listMethod = controllerType.GetMethod(nameof(CurriculumsController.ListCurriculums));
        Assert.NotNull(listMethod);

        Assert.NotNull(listMethod.GetCustomAttribute<HttpGetAttribute>());

        var listAuthAttr = listMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(listAuthAttr);
        Assert.Equal("curriculum.curriculums.read", listAuthAttr.Policy);

        var listProducesAttrs = listMethod.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();
        Assert.Equal(3, listProducesAttrs.Count);
        Assert.NotNull(listProducesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status200OK && a.Type == typeof(CurriculumListResponse)));
        Assert.NotNull(listProducesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status400BadRequest && a.Type == typeof(ProblemDetails)));
        Assert.NotNull(listProducesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status404NotFound && a.Type == typeof(ProblemDetails)));
    }

    [Fact]
    public void CurriculumAndQuestionsDependencyInjection_RegistersUseCaseScoped()
    {
        var services = new ServiceCollection();
        services.AddCurriculumAndQuestions();

        var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ICreateCurriculumUseCase));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(CreateCurriculumUseCase), descriptor.ImplementationType);
    }

    [Fact]
    public void ProgramCs_CallsAddCurriculumAndQuestions_ExactlyOnce()
    {
        var programCsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.API", "Program.cs");
        var fullPath = Path.GetFullPath(programCsPath);

        Assert.True(File.Exists(fullPath), $"Program.cs not found at path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        var matches = System.Text.RegularExpressions.Regex.Matches(content, @"AddCurriculumAndQuestions\(\)");

        Assert.Single(matches);
    }

    [Fact]
    public async Task ListCurriculums_Success_Returns200NonPaginatedEnvelope()
    {
        var query = new CurriculumListQuery();
        var dtos = new List<CurriculumDto>
        {
            new CurriculumDto { CurriculumId = Guid.NewGuid().ToString("D"), Title = "C1" },
            new CurriculumDto { CurriculumId = Guid.NewGuid().ToString("D"), Title = "C2" }
        };

        _listUseCaseMock
            .Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListCurriculumsResult.Success(dtos));

        var result = await _sut.ListCurriculums(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CurriculumListResponse>(okResult.Value);

        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.Count);
        Assert.Equal("C1", response.Data[0].Title);
        Assert.Equal("C2", response.Data[1].Title);
        Assert.NotNull(response.Meta);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
        Assert.Equal(_fixedTime.UtcDateTime, response.Meta.Timestamp);
    }

    [Fact]
    public async Task ListCurriculums_EmptyResult_Returns200WithEmptyData()
    {
        var query = new CurriculumListQuery();

        _listUseCaseMock
            .Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListCurriculumsResult.Success(new List<CurriculumDto>()));

        var result = await _sut.ListCurriculums(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CurriculumListResponse>(okResult.Value);

        Assert.NotNull(response.Data);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task ListCurriculums_PassesExactQueryAndCancellationToken()
    {
        var query = new CurriculumListQuery { SubjectId = Guid.NewGuid() };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _listUseCaseMock
            .Setup(x => x.ExecuteAsync(It.IsAny<CurriculumListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListCurriculumsResult.Success(new List<CurriculumDto>()));

        await _sut.ListCurriculums(query, token);

        _listUseCaseMock.Verify(x => x.ExecuteAsync(query, token), Times.Once);
    }

    [Fact]
    public async Task ListCurriculums_ValidationFailed_Returns400ProblemDetails()
    {
        var query = new CurriculumListQuery();

        _listUseCaseMock
            .Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListCurriculumsResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.ListCurriculums(query, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);

        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("Dữ liệu không hợp lệ", problemDetails.Title);
        Assert.Equal("/api/v1/curriculums", problemDetails.Instance);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task ListCurriculums_ResourceNotFound_Returns404ProblemDetails()
    {
        var query = new CurriculumListQuery();

        _listUseCaseMock
            .Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListCurriculumsResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _sut.ListCurriculums(query, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);

        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Không tìm thấy dữ liệu", problemDetails.Title);
        Assert.Equal("/api/v1/curriculums", problemDetails.Instance);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
    }

    [Fact]
    public async Task ListCurriculums_UnexpectedErrorCode_ThrowsInvalidOperationException()
    {
        var query = new CurriculumListQuery();

        _listUseCaseMock
            .Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListCurriculumsResult.Failure("UNEXPECTED"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ListCurriculums(query, CancellationToken.None));

        Assert.Contains("UNEXPECTED", ex.Message);
    }

    [Fact]
    public void CurriculumListResponse_HasNoPaginationProperties()
    {
        var type = typeof(CurriculumListResponse);
        var properties = type.GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        Assert.DoesNotContain("page", properties);
        Assert.DoesNotContain("pagesize", properties);
        Assert.DoesNotContain("totalitems", properties);
        Assert.DoesNotContain("totalpages", properties);
        Assert.DoesNotContain("issuccess", properties);
        Assert.DoesNotContain("message", properties);
    }
}
