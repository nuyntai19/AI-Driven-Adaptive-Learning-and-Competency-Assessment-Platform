using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using EduTwin.API.Controllers;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

public sealed class LearningControllerTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 15, 8, 30, 45, 123, TimeSpan.Zero);

    private const string TraceId = "00-abcd-1234-01";
    private const string RequestPath = "/api/v1/learning/attempts";

    private readonly Mock<ISubmitAttemptUseCase> _useCase = new();
    private readonly Mock<TimeProvider> _timeProvider = new();

    public LearningControllerTests()
    {
        _timeProvider.Setup(provider => provider.GetUtcNow()).Returns(FixedNow);
    }

    [Fact]
    public async Task SubmitAttempt_SuccessReturnsExactAcceptedEnvelope()
    {
        var request = CreateRequest();
        using var cancellation = new CancellationTokenSource();
        var data = CreateAcceptedData();
        var controller = CreateController();
        var expectedCorrelationId = Activity.Current?.Id ?? TraceId;
        _useCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<SubmitAttemptRequest>(candidate => ReferenceEquals(candidate, request)),
                expectedCorrelationId,
                cancellation.Token))
            .ReturnsAsync(SubmitAttemptResult.Success(data));

        var actionResult = await controller.SubmitAttempt(request, cancellation.Token);

        var accepted = Assert.IsType<AcceptedResult>(actionResult);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Null(accepted.Location);
        var response = Assert.IsType<SubmitAttemptResponse>(accepted.Value);
        Assert.Same(data, response.Data);
        Assert.Equal(expectedCorrelationId, response.Meta.TraceId);
        Assert.Equal(FixedNow.UtcDateTime, response.Meta.Timestamp);
        Assert.Equal("12001", response.Data.AttemptId);
        Assert.Equal("13001", response.Data.AnalysisJobId);
        Assert.Equal("PendingAnalysis", response.Data.AttemptStatus);
        Assert.Equal("Pending", response.Data.JobStatus);
        Assert.Equal("/api/v1/learning/analysis-jobs/13001", response.Data.PollUrl);
        Assert.Equal(3000, response.Data.PollAfterMilliseconds);

        var json = JsonSerializer.SerializeToElement(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(2, json.EnumerateObject().Count());
        Assert.Equal(6, json.GetProperty("data").EnumerateObject().Count());
        Assert.Equal(2, json.GetProperty("meta").EnumerateObject().Count());
        Assert.Equal("12001", json.GetProperty("data").GetProperty("attemptId").GetString());
        Assert.Equal("13001", json.GetProperty("data").GetProperty("analysisJobId").GetString());
    }

    [Fact]
    public async Task SubmitAttempt_PassesExactRequestCorrelationAndCancellationToken()
    {
        var request = CreateRequest();
        using var cancellation = new CancellationTokenSource();
        var controller = CreateController();
        var expectedCorrelationId = Activity.Current?.Id ?? TraceId;
        _useCase
            .Setup(useCase => useCase.ExecuteAsync(
                request,
                expectedCorrelationId,
                cancellation.Token))
            .ReturnsAsync(SubmitAttemptResult.Success(CreateAcceptedData()));

        await controller.SubmitAttempt(request, cancellation.Token);

        _useCase.Verify(useCase => useCase.ExecuteAsync(
            It.Is<SubmitAttemptRequest>(candidate => ReferenceEquals(candidate, request)),
            expectedCorrelationId,
            cancellation.Token), Times.Once);
        _useCase.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ErrorCodes.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCodes.ResourceNotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCodes.DuplicateSubmission, StatusCodes.Status409Conflict)]
    [InlineData(ErrorCodes.AssignmentNotAvailable, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(ErrorCodes.QuestionReasoningRequired, StatusCodes.Status422UnprocessableEntity)]
    public async Task SubmitAttempt_MapsKnownErrorsToProblemDetails(
        string errorCode,
        int expectedStatus)
    {
        var request = CreateRequest();
        var controller = CreateController();
        _useCase
            .Setup(useCase => useCase.ExecuteAsync(
                request,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubmitAttemptResult.Failure(errorCode));

        var actionResult = await controller.SubmitAttempt(request, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(actionResult);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.Equal(RequestPath, problem.Instance);
        Assert.Equal(Activity.Current?.Id ?? TraceId, problem.Extensions["traceId"]);
        Assert.Equal(errorCode, problem.Extensions["errorCode"]);
    }

    [Theory]
    [InlineData("UNKNOWN_ERROR")]
    [InlineData(null)]
    public async Task SubmitAttempt_UnknownOrNullErrorCodeThrows(string? errorCode)
    {
        var request = CreateRequest();
        var controller = CreateController();
        _useCase
            .Setup(useCase => useCase.ExecuteAsync(
                request,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubmitAttemptResult.Failure(errorCode!));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.SubmitAttempt(request, CancellationToken.None));
    }

    [Fact]
    public void SubmitAttempt_HasLockedRoutePolicyAndResponseMetadata()
    {
        var controllerType = typeof(LearningController);
        var route = Assert.Single(controllerType.GetCustomAttributes<RouteAttribute>());
        Assert.Equal("api/v1/learning", route.Template);
        Assert.Single(controllerType.GetCustomAttributes<AuthorizeAttribute>());

        var method = controllerType.GetMethod(nameof(LearningController.SubmitAttempt));
        Assert.NotNull(method);
        var post = Assert.Single(method.GetCustomAttributes<HttpPostAttribute>());
        Assert.Equal("attempts", post.Template);
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.StudentOnly, authorization.Policy);

        var responses = method.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();
        Assert.Contains(responses, response =>
            response.StatusCode == StatusCodes.Status202Accepted &&
            response.Type == typeof(SubmitAttemptResponse));
        foreach (var status in new[]
                 {
                     StatusCodes.Status400BadRequest,
                     StatusCodes.Status404NotFound,
                     StatusCodes.Status409Conflict,
                     StatusCodes.Status422UnprocessableEntity
                 })
        {
            Assert.Contains(responses, response =>
                response.StatusCode == status &&
                response.Type == typeof(ProblemDetails));
        }

        Assert.Equal(2, typeof(SubmitAttemptResponse).GetProperties().Length);
        Assert.Equal(6, typeof(SubmitAttemptAcceptedDataDto).GetProperties().Length);
    }

    private LearningController CreateController()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };
        context.Request.Path = RequestPath;
        return new LearningController(_useCase.Object, _timeProvider.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static SubmitAttemptRequest CreateRequest() =>
        new()
        {
            ClientSubmissionId = Guid.NewGuid(),
            QuestionId = "9001",
            AssignmentId = Guid.NewGuid(),
            FinalAnswer = "B",
            ReasoningText = "Giải thích",
            TimeSpentSeconds = 165,
            Confidence = 80,
            AnswerChanges = 1,
            Skipped = false
        };

    private static SubmitAttemptAcceptedDataDto CreateAcceptedData() =>
        new()
        {
            AttemptId = "12001",
            AnalysisJobId = "13001",
            AttemptStatus = "PendingAnalysis",
            JobStatus = "Pending",
            PollUrl = "/api/v1/learning/analysis-jobs/13001",
            PollAfterMilliseconds = 3000
        };
}
