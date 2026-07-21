using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.Controllers;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.Tests.DigitalTwin;

public class StudentSubjectGoalsControllerTests
{
    private readonly Mock<IUpsertStudentSubjectGoalUseCase> _mockUseCase;
    private readonly Mock<IListStudentsUseCase> _mockList;
    private readonly Mock<IGetStudentUseCase> _mockGet;
    private readonly Mock<ICreateStudentUseCase> _mockCreate;
    private readonly Mock<IUpdateStudentUseCase> _mockUpdate;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly StudentsController _controller;
    private readonly DateTimeOffset _fixedUtcNow;
    private readonly string _testTraceId;
    private readonly string _testPath;

    public StudentSubjectGoalsControllerTests()
    {
        _mockUseCase = new Mock<IUpsertStudentSubjectGoalUseCase>();
        _mockList = new Mock<IListStudentsUseCase>();
        _mockGet = new Mock<IGetStudentUseCase>();
        _mockCreate = new Mock<ICreateStudentUseCase>();
        _mockUpdate = new Mock<IUpdateStudentUseCase>();
        _mockTimeProvider = new Mock<TimeProvider>();

        _fixedUtcNow = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(_fixedUtcNow);

        _testTraceId = "test-trace-id";
        _testPath = "/api/v1/students/123/goals/456";

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = _testTraceId
        };
        httpContext.Request.Path = _testPath;

        _controller = new StudentsController(
            _mockList.Object,
            _mockGet.Object,
            _mockCreate.Object,
            _mockUpdate.Object,
            _mockUseCase.Object,
            _mockTimeProvider.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task UpsertStudentSubjectGoal_Success_ReturnsOkWithResponse()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var request = new UpsertStudentSubjectGoalRequest { TargetScore = 8.5m, RemainingDays = 30, RowVersion = "1" };
        var goalDto = new StudentSubjectGoalDto
        {
            GoalId = "1234567890",
            StudentId = studentId.ToString(),
            SubjectId = subjectId.ToString(),
            TargetScore = 8.5m,
            RemainingDays = 30,
            CurrentPredictedScore = 8.0m,
            RiskScore = 0.5m,
            RowVersion = "2"
        };
        var cts = new CancellationTokenSource();

        _mockUseCase.Setup(u => u.ExecuteAsync(studentId, subjectId, request, cts.Token))
            .ReturnsAsync(UpsertStudentSubjectGoalResult.Success(goalDto));

        // Act
        var result = await _controller.UpsertStudentSubjectGoal(studentId, subjectId, request, cts.Token);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var response = Assert.IsType<StudentSubjectGoalResponse>(okResult.Value);
        Assert.Same(goalDto, response.Data);

        Assert.NotNull(response.Meta);
        Assert.Equal(_testTraceId, response.Meta.TraceId);
        Assert.Equal(_fixedUtcNow.UtcDateTime, response.Meta.Timestamp);

        _mockUseCase.Verify(u => u.ExecuteAsync(
            studentId,
            subjectId,
            It.Is<UpsertStudentSubjectGoalRequest>(r => ReferenceEquals(r, request)),
            cts.Token), Times.Once);
    }

    [Fact]
    public async Task UpsertStudentSubjectGoal_ValidationFailed_ReturnsBadRequest()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var request = new UpsertStudentSubjectGoalRequest();
        var cts = new CancellationTokenSource();

        _mockUseCase.Setup(u => u.ExecuteAsync(studentId, subjectId, request, cts.Token))
            .ReturnsAsync(UpsertStudentSubjectGoalResult.ValidationFailed());

        // Act
        var result = await _controller.UpsertStudentSubjectGoal(studentId, subjectId, request, cts.Token);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1", problemDetails.Type);
        Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Title));
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
        Assert.Equal(_testPath, problemDetails.Instance);
        Assert.Equal("VALIDATION_FAILED", problemDetails.Extensions["errorCode"]);
        Assert.Equal(_testTraceId, problemDetails.Extensions["traceId"]);

        _mockUseCase.Verify(u => u.ExecuteAsync(
            studentId,
            subjectId,
            It.Is<UpsertStudentSubjectGoalRequest>(r => ReferenceEquals(r, request)),
            cts.Token), Times.Once);
    }

    [Fact]
    public async Task UpsertStudentSubjectGoal_ForbiddenResource_ReturnsForbidden()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var request = new UpsertStudentSubjectGoalRequest();
        var cts = new CancellationTokenSource();

        _mockUseCase.Setup(u => u.ExecuteAsync(studentId, subjectId, request, cts.Token))
            .ReturnsAsync(UpsertStudentSubjectGoalResult.Forbidden());

        // Act
        var result = await _controller.UpsertStudentSubjectGoal(studentId, subjectId, request, cts.Token);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4", problemDetails.Type);
        Assert.Equal(StatusCodes.Status403Forbidden, problemDetails.Status);
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Title));
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
        Assert.Equal(_testPath, problemDetails.Instance);
        Assert.Equal("FORBIDDEN_RESOURCE", problemDetails.Extensions["errorCode"]);
        Assert.Equal(_testTraceId, problemDetails.Extensions["traceId"]);

        _mockUseCase.Verify(u => u.ExecuteAsync(
            studentId,
            subjectId,
            It.Is<UpsertStudentSubjectGoalRequest>(r => ReferenceEquals(r, request)),
            cts.Token), Times.Once);
    }

    [Fact]
    public async Task UpsertStudentSubjectGoal_ResourceNotFound_ReturnsNotFound()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var request = new UpsertStudentSubjectGoalRequest();
        var cts = new CancellationTokenSource();

        _mockUseCase.Setup(u => u.ExecuteAsync(studentId, subjectId, request, cts.Token))
            .ReturnsAsync(UpsertStudentSubjectGoalResult.NotFound());

        // Act
        var result = await _controller.UpsertStudentSubjectGoal(studentId, subjectId, request, cts.Token);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFoundResult.StatusCode);

        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5", problemDetails.Type);
        Assert.Equal(StatusCodes.Status404NotFound, problemDetails.Status);
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Title));
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
        Assert.Equal(_testPath, problemDetails.Instance);
        Assert.Equal("RESOURCE_NOT_FOUND", problemDetails.Extensions["errorCode"]);
        Assert.Equal(_testTraceId, problemDetails.Extensions["traceId"]);

        _mockUseCase.Verify(u => u.ExecuteAsync(
            studentId,
            subjectId,
            It.Is<UpsertStudentSubjectGoalRequest>(r => ReferenceEquals(r, request)),
            cts.Token), Times.Once);
    }

    [Fact]
    public async Task UpsertStudentSubjectGoal_ConcurrencyConflict_ReturnsConflict()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var request = new UpsertStudentSubjectGoalRequest();
        var cts = new CancellationTokenSource();

        _mockUseCase.Setup(u => u.ExecuteAsync(studentId, subjectId, request, cts.Token))
            .ReturnsAsync(UpsertStudentSubjectGoalResult.Conflict());

        // Act
        var result = await _controller.UpsertStudentSubjectGoal(studentId, subjectId, request, cts.Token);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflictResult.StatusCode);

        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10", problemDetails.Type);
        Assert.Equal(StatusCodes.Status409Conflict, problemDetails.Status);
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Title));
        Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
        Assert.Equal(_testPath, problemDetails.Instance);
        Assert.Equal("CONCURRENCY_CONFLICT", problemDetails.Extensions["errorCode"]);
        Assert.Equal(_testTraceId, problemDetails.Extensions["traceId"]);

        _mockUseCase.Verify(u => u.ExecuteAsync(
            studentId,
            subjectId,
            It.Is<UpsertStudentSubjectGoalRequest>(r => ReferenceEquals(r, request)),
            cts.Token), Times.Once);
    }

    [Fact]
    public async Task UpsertStudentSubjectGoal_UnknownErrorCode_ThrowsInvalidOperationException()
    {
        // Arrange
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var request = new UpsertStudentSubjectGoalRequest();
        var cts = new CancellationTokenSource();

        var unknownResult = UpsertStudentSubjectGoalResult.Failure("UNKNOWN_ERROR");
        _mockUseCase.Setup(u => u.ExecuteAsync(studentId, subjectId, request, cts.Token))
            .ReturnsAsync(unknownResult);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpsertStudentSubjectGoal(studentId, subjectId, request, cts.Token));

        _mockUseCase.Verify(u => u.ExecuteAsync(
            studentId,
            subjectId,
            It.Is<UpsertStudentSubjectGoalRequest>(r => ReferenceEquals(r, request)),
            cts.Token), Times.Once);
    }

    [Fact]
    public void DependencyInjection_AddDigitalTwin_RegistersExpectedDescriptors()
    {
        var services = new ServiceCollection();
        services.AddDigitalTwin();

        var generatorDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IGoalIdGenerator));
        Assert.NotNull(generatorDescriptor);
        Assert.Equal(typeof(CryptographicGoalIdGenerator), generatorDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, generatorDescriptor.Lifetime);

        var useCaseDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUpsertStudentSubjectGoalUseCase));
        Assert.NotNull(useCaseDescriptor);
        Assert.Equal(typeof(UpsertStudentSubjectGoalUseCase), useCaseDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, useCaseDescriptor.Lifetime);
    }

    [Fact]
    public void UpsertStudentSubjectGoal_HasRequiredAttributes()
    {
        var method = typeof(StudentsController).GetMethod(nameof(StudentsController.UpsertStudentSubjectGoal));

        Assert.NotNull(method);

        var httpPutAttrs = method.GetCustomAttributes(typeof(HttpPutAttribute), false).Cast<HttpPutAttribute>().ToList();
        Assert.Single(httpPutAttrs);
        Assert.Equal("{studentId}/goals/{subjectId}", httpPutAttrs[0].Template);

        var authorizeAttrs = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().ToList();
        Assert.Single(authorizeAttrs);
        Assert.Null(authorizeAttrs[0].Policy);
    }
}
