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
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CurriculumsController _sut;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 7, 24, 14, 0, 0, TimeSpan.Zero);

    public CurriculumsControllerTests()
    {
        _createUseCaseMock = new Mock<ICreateCurriculumUseCase>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _sut = new CurriculumsController(_createUseCaseMock.Object, _timeProviderMock.Object);

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

        // Method level attributes
        var method = controllerType.GetMethod(nameof(CurriculumsController.CreateCurriculum));
        Assert.NotNull(method);

        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());

        var authAttr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal(AuthorizationPolicies.TeacherOrCenterManager, authAttr.Policy);

        var producesAttrs = method.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();
        Assert.Equal(3, producesAttrs.Count);

        var produces201 = producesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status201Created);
        Assert.NotNull(produces201);
        Assert.Equal(typeof(CurriculumResponse), produces201.Type);

        var produces400 = producesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status400BadRequest);
        Assert.NotNull(produces400);
        Assert.Equal(typeof(ProblemDetails), produces400.Type);

        var produces404 = producesAttrs.FirstOrDefault(a => a.StatusCode == StatusCodes.Status404NotFound);
        Assert.NotNull(produces404);
        Assert.Equal(typeof(ProblemDetails), produces404.Type);
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
}
