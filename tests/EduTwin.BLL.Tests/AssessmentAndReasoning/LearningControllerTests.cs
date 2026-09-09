using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using EduTwin.API.Controllers;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.AssessmentAndReasoning.Polling;
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
    private const string AnalysisJobRequestPath =
        "/api/v1/learning/analysis-jobs/13001";

    private readonly Mock<ISubmitAttemptUseCase> _useCase = new();
    private readonly Mock<IListAttemptsUseCase> _listAttemptsUseCase = new();
    private readonly Mock<IGetAnalysisJobStatusUseCase> _jobStatusUseCase = new();
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
        var authorizationPolicies = method
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(authorizationPolicies.SetEquals(
        [
            AuthorizationPolicies.StudentOnly,
            "learning.attempts.submit"
        ]));

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

    [Fact]
    public async Task GetAnalysisJobStatus_SuccessReturnsExactOkEnvelope()
    {
        using var cancellation = new CancellationTokenSource();
        var controller = CreateController(AnalysisJobRequestPath);
        var data = CreateJobStatusData();
        _jobStatusUseCase
            .Setup(useCase => useCase.ExecuteAsync("13001", cancellation.Token))
            .ReturnsAsync(GetAnalysisJobStatusResult.Success(data));

        var actionResult = await controller.GetAnalysisJobStatus(
            "13001",
            cancellation.Token);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        var response = Assert.IsType<AnalysisJobStatusResponse>(ok.Value);
        Assert.Same(data, response.Data);
        Assert.Equal(Activity.Current?.Id ?? TraceId, response.Meta.TraceId);
        Assert.Equal(FixedNow.UtcDateTime, response.Meta.Timestamp);

        var json = JsonSerializer.SerializeToElement(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(2, json.EnumerateObject().Count());
        Assert.Equal(7, json.GetProperty("data").EnumerateObject().Count());
        Assert.Equal(2, json.GetProperty("meta").EnumerateObject().Count());
        Assert.Equal(
            "13001",
            json.GetProperty("data").GetProperty("analysisJobId").GetString());
        Assert.False(json.GetProperty("data").GetProperty("terminal").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("data").GetProperty("feedbackUrl").ValueKind);
    }

    [Fact]
    public async Task GetAnalysisJobStatus_PassesExactRouteIdAndCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var controller = CreateController(AnalysisJobRequestPath);
        _jobStatusUseCase
            .Setup(useCase => useCase.ExecuteAsync("13001", cancellation.Token))
            .ReturnsAsync(GetAnalysisJobStatusResult.Success(CreateJobStatusData()));

        await controller.GetAnalysisJobStatus("13001", cancellation.Token);

        _jobStatusUseCase.Verify(useCase => useCase.ExecuteAsync(
            "13001",
            cancellation.Token), Times.Once);
        _jobStatusUseCase.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ErrorCodes.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCodes.ForbiddenResource, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCodes.ResourceNotFound, StatusCodes.Status404NotFound)]
    public async Task GetAnalysisJobStatus_MapsKnownErrorsToProblemDetails(
        string errorCode,
        int expectedStatus)
    {
        var controller = CreateController(AnalysisJobRequestPath);
        _jobStatusUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                "13001",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorCode switch
            {
                ErrorCodes.ValidationFailed => GetAnalysisJobStatusResult.ValidationFailed(),
                ErrorCodes.ForbiddenResource => GetAnalysisJobStatusResult.Forbidden(),
                ErrorCodes.ResourceNotFound => GetAnalysisJobStatusResult.NotFound(),
                _ => throw new InvalidOperationException()
            });

        var actionResult = await controller.GetAnalysisJobStatus(
            "13001",
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(actionResult);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.Equal(AnalysisJobRequestPath, problem.Instance);
        Assert.Equal(Activity.Current?.Id ?? TraceId, problem.Extensions["traceId"]);
        Assert.Equal(errorCode, problem.Extensions["errorCode"]);
    }

    [Theory]
    [InlineData("UNKNOWN_ERROR")]
    [InlineData(null)]
    public async Task GetAnalysisJobStatus_UnknownOrNullErrorCodeThrows(
        string? errorCode)
    {
        var controller = CreateController(AnalysisJobRequestPath);
        _jobStatusUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                "13001",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetAnalysisJobStatusResult.Failure(errorCode!));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.GetAnalysisJobStatus("13001", CancellationToken.None));
    }

    [Fact]
    public void GetAnalysisJobStatus_HasLockedRouteAndResponseMetadata()
    {
        var method = typeof(LearningController).GetMethod(
            nameof(LearningController.GetAnalysisJobStatus));
        Assert.NotNull(method);
        var get = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal("analysis-jobs/{analysisJobId}", get.Template);
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(
            EduTwin.API.Security.CompositePermissionPolicies.AttemptsRead,
            authorization.Policy);

        var responses = method.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();
        Assert.Contains(responses, response =>
            response.StatusCode == StatusCodes.Status200OK
            && response.Type == typeof(AnalysisJobStatusResponse));
        foreach (var status in new[]
                 {
                     StatusCodes.Status400BadRequest,
                     StatusCodes.Status403Forbidden,
                     StatusCodes.Status404NotFound
                 })
        {
            Assert.Contains(responses, response =>
                response.StatusCode == status
                && response.Type == typeof(ProblemDetails));
        }

        Assert.Equal(2, typeof(AnalysisJobStatusResponse).GetProperties().Length);
        Assert.Equal(7, typeof(AnalysisJobStatusDataDto).GetProperties().Length);
    }

    [Fact]
    public async Task ListAttempts_SuccessReturnsExactOkEnvelopeAndNormalizedMeta()
    {
        var query = new ListAttemptsQuery { Page = "01", PageSize = "020" };
        using var cancellation = new CancellationTokenSource();
        var controller = CreateController(RequestPath);
        var item = CreateAttemptSummary();
        _listAttemptsUseCase
            .Setup(useCase => useCase.ExecuteAsync(query, cancellation.Token))
            .ReturnsAsync(ListAttemptsResult.Success([item], 1, 20, 21, 2));

        var actionResult = await controller.ListAttempts(query, cancellation.Token);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<AttemptListResponse>(ok.Value);
        Assert.Same(item, Assert.Single(response.Data));
        Assert.Equal(1, response.Meta.Page);
        Assert.Equal(20, response.Meta.PageSize);
        Assert.Equal(21, response.Meta.TotalItems);
        Assert.Equal(2, response.Meta.TotalPages);
        Assert.Equal(Activity.Current?.Id ?? TraceId, response.Meta.TraceId);
        Assert.Equal(FixedNow.UtcDateTime, response.Meta.Timestamp);

        var json = JsonSerializer.SerializeToElement(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(2, json.EnumerateObject().Count());
        Assert.Equal(15, json.GetProperty("data")[0].EnumerateObject().Count());
        Assert.Equal(6, json.GetProperty("meta").EnumerateObject().Count());
    }

    [Fact]
    public async Task ListAttempts_PassesSameQueryObjectAndExactCancellationToken()
    {
        var query = new ListAttemptsQuery { Status = "Completed" };
        using var cancellation = new CancellationTokenSource();
        var controller = CreateController(RequestPath);
        _listAttemptsUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.Is<ListAttemptsQuery>(candidate => ReferenceEquals(candidate, query)),
                cancellation.Token))
            .ReturnsAsync(ListAttemptsResult.Success([], 1, 20, 0, 0));

        await controller.ListAttempts(query, cancellation.Token);

        _listAttemptsUseCase.Verify(useCase => useCase.ExecuteAsync(
            It.Is<ListAttemptsQuery>(candidate => ReferenceEquals(candidate, query)),
            cancellation.Token), Times.Once);
        _listAttemptsUseCase.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ErrorCodes.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCodes.ForbiddenResource, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCodes.ResourceNotFound, StatusCodes.Status404NotFound)]
    public async Task ListAttempts_MapsKnownErrorsToProblemDetails(
        string errorCode,
        int expectedStatus)
    {
        var query = new ListAttemptsQuery();
        var controller = CreateController(RequestPath);
        _listAttemptsUseCase
            .Setup(useCase => useCase.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorCode switch
            {
                ErrorCodes.ValidationFailed => ListAttemptsResult.ValidationFailed(),
                ErrorCodes.ForbiddenResource => ListAttemptsResult.Forbidden(),
                ErrorCodes.ResourceNotFound => ListAttemptsResult.NotFound(),
                _ => throw new InvalidOperationException()
            });

        var actionResult = await controller.ListAttempts(query, CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(actionResult);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatus, problem.Status);
        Assert.Equal(RequestPath, problem.Instance);
        Assert.Equal(Activity.Current?.Id ?? TraceId, problem.Extensions["traceId"]);
        Assert.Equal(errorCode, problem.Extensions["errorCode"]);
    }

    [Theory]
    [InlineData("UNKNOWN_ERROR")]
    [InlineData(null)]
    public async Task ListAttempts_UnknownOrNullErrorCodeThrows(string? errorCode)
    {
        var query = new ListAttemptsQuery();
        var controller = CreateController(RequestPath);
        _listAttemptsUseCase
            .Setup(useCase => useCase.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListAttemptsResult.Failure(errorCode!));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.ListAttempts(query, CancellationToken.None));
    }

    [Fact]
    public void ListAttempts_HasLockedRouteBindingAuthorizationAndResponseMetadata()
    {
        var method = typeof(LearningController).GetMethod(nameof(LearningController.ListAttempts));
        Assert.NotNull(method);
        var get = Assert.Single(method.GetCustomAttributes<HttpGetAttribute>());
        Assert.Equal("attempts", get.Template);
        var authorization = Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(
            EduTwin.API.Security.CompositePermissionPolicies.AttemptsRead,
            authorization.Policy);

        var queryParameter = Assert.Single(
            method.GetParameters(),
            parameter => parameter.ParameterType == typeof(ListAttemptsQuery));
        Assert.NotNull(queryParameter.GetCustomAttribute<FromQueryAttribute>());

        var responses = method.GetCustomAttributes<ProducesResponseTypeAttribute>().ToList();
        Assert.Contains(responses, response =>
            response.StatusCode == StatusCodes.Status200OK &&
            response.Type == typeof(AttemptListResponse));
        foreach (var status in new[]
                 {
                     StatusCodes.Status400BadRequest,
                     StatusCodes.Status403Forbidden,
                     StatusCodes.Status404NotFound
                 })
        {
            Assert.Contains(responses, response =>
                response.StatusCode == status && response.Type == typeof(ProblemDetails));
        }
    }

    private LearningController CreateController(string requestPath = RequestPath)
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };
        context.Request.Path = requestPath;
        return new LearningController(
            _useCase.Object,
            _listAttemptsUseCase.Object,
            _jobStatusUseCase.Object,
            _timeProvider.Object)
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

    private static AnalysisJobStatusDataDto CreateJobStatusData() =>
        new()
        {
            AnalysisJobId = "13001",
            AttemptId = "12001",
            Status = "Processing",
            RetryCount = 0,
            Terminal = false,
            FeedbackUrl = null,
            UpdatedAt = FixedNow.UtcDateTime
        };

    private static AttemptSummaryDto CreateAttemptSummary() =>
        new()
        {
            AttemptId = "12001",
            StudentId = "baf68743-a272-4983-a9e2-41663734a7c2",
            StudentName = "Trần Minh An",
            SubjectId = "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
            QuestionId = "9001",
            QuestionText = "Question",
            AssignmentId = null,
            AttemptStatus = "Completed",
            Grading = new AttemptSummaryGradingDto
            {
                IsCorrect = true,
                AwardedScore = 1,
                MaxScore = 1,
                Skipped = false
            },
            AnalysisJobId = "13001",
            JobStatus = "Completed",
            Terminal = true,
            PollUrl = "/api/v1/learning/analysis-jobs/13001",
            CreatedAt = FixedNow.UtcDateTime,
            UpdatedAt = FixedNow.UtcDateTime
        };
}
