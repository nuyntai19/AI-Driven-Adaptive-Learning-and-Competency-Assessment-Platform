using EduTwin.API.Controllers;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.ReviewQueue;

public sealed class TeacherReviewControllerTests
{
    private static readonly DateTime UtcNow =
        new(2026, 9, 10, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListReviewQueue_Success_MapsPagedEnvelopeAndPassesToken()
    {
        var useCase = new StubUseCase(ListTeacherReviewQueueResult.Success(
            [new TeacherReviewQueueItemDto { AttemptId = "7" }],
            2,
            10,
            11,
            2));
        var controller = CreateController(useCase);
        using var cancellation = new CancellationTokenSource();
        var query = new TeacherReviewQueueQuery { Page = 2, PageSize = 10 };

        var action = await controller.ListReviewQueue(query, cancellation.Token);

        var ok = Assert.IsType<OkObjectResult>(action);
        var response = Assert.IsType<TeacherReviewQueueResponse>(ok.Value);
        Assert.Equal("7", Assert.Single(response.Data).AttemptId);
        Assert.Equal(2, response.Meta.Page);
        Assert.Equal(10, response.Meta.PageSize);
        Assert.Equal(11, response.Meta.TotalItems);
        Assert.Equal(2, response.Meta.TotalPages);
        Assert.Equal("trace-review", response.Meta.TraceId);
        Assert.Equal(UtcNow, response.Meta.Timestamp);
        Assert.Same(query, useCase.Query);
        Assert.Equal(cancellation.Token, useCase.Token);
    }

    [Theory]
    [InlineData(ErrorCodes.ValidationFailed, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCodes.ForbiddenResource, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCodes.ResourceNotFound, StatusCodes.Status404NotFound)]
    public async Task ListReviewQueue_Failure_MapsProblemDetails(string errorCode, int expectedStatus)
    {
        var result = errorCode switch
        {
            ErrorCodes.ValidationFailed => ListTeacherReviewQueueResult.ValidationFailed(),
            ErrorCodes.ForbiddenResource => ListTeacherReviewQueueResult.Forbidden(),
            _ => ListTeacherReviewQueueResult.NotFound()
        };
        var controller = CreateController(new StubUseCase(result));

        var action = await controller.ListReviewQueue(
            new TeacherReviewQueueQuery(),
            CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(action);
        Assert.Equal(expectedStatus, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(errorCode, problem.Extensions["errorCode"]);
        Assert.Equal("trace-review", problem.Extensions["traceId"]);
    }

    [Fact]
    public void Controller_UsesExactRouteAndAuthorizationPolicies()
    {
        var type = typeof(TeacherReviewController);
        var route = Assert.Single(type.GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>());
        var policies = type.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();
        var method = type.GetMethod(nameof(TeacherReviewController.ListReviewQueue))!;
        var httpGet = Assert.Single(method.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>());

        Assert.Equal("api/v1/teachers/me", route.Template);
        Assert.Equal("review-queue", httpGet.Template);
        Assert.Contains(AuthorizationPolicies.TeacherOnly, policies);
        Assert.Contains("twin.reasoning.review", policies);
    }

    private static TeacherReviewController CreateController(StubUseCase useCase)
    {
        var controller = new TeacherReviewController(useCase, new FixedTimeProvider(UtcNow));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-review" }
        };
        return controller;
    }

    private sealed class StubUseCase : IListTeacherReviewQueueUseCase
    {
        private readonly ListTeacherReviewQueueResult _result;

        public StubUseCase(ListTeacherReviewQueueResult result)
        {
            _result = result;
        }

        public TeacherReviewQueueQuery? Query { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<ListTeacherReviewQueueResult> ExecuteAsync(
            TeacherReviewQueueQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            Token = cancellationToken;
            return Task.FromResult(_result);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
