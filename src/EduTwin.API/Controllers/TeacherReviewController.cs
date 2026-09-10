using System.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/teachers/me")]
[Authorize(Policy = AuthorizationPolicies.TeacherOnly)]
[Authorize(Policy = "twin.reasoning.review")]
public sealed class TeacherReviewController : ControllerBase
{
    private readonly IListTeacherReviewQueueUseCase _useCase;
    private readonly TimeProvider _timeProvider;

    public TeacherReviewController(
        IListTeacherReviewQueueUseCase useCase,
        TimeProvider timeProvider)
    {
        _useCase = useCase;
        _timeProvider = timeProvider;
    }

    [HttpGet("review-queue")]
    [Authorize(Policy = "twin.reasoning.review")]
    [ProducesResponseType(typeof(TeacherReviewQueueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListReviewQueue(
        [FromQuery] TeacherReviewQueueQuery query,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _useCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new TeacherReviewQueueResponse
            {
                Data = result.Data ?? throw new InvalidOperationException(
                    "Review queue succeeded without response data."),
                Meta = new PagedMetaDto
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages,
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return result.ErrorCode switch
        {
            ErrorCodes.ValidationFailed => ProblemResponse(
                StatusCodes.Status400BadRequest,
                "validation",
                "Dữ liệu không hợp lệ",
                "Bộ lọc hàng đợi cần duyệt không hợp lệ.",
                ErrorCodes.ValidationFailed,
                traceId),
            ErrorCodes.ForbiddenResource => ProblemResponse(
                StatusCodes.Status403Forbidden,
                "forbidden",
                "Không có quyền truy cập",
                "Lớp học không thuộc phạm vi phụ trách của giáo viên hiện tại.",
                ErrorCodes.ForbiddenResource,
                traceId),
            ErrorCodes.ResourceNotFound => ProblemResponse(
                StatusCodes.Status404NotFound,
                "not-found",
                "Không tìm thấy dữ liệu",
                "Không tìm thấy giáo viên hoặc lớp học trong trung tâm hiện tại.",
                ErrorCodes.ResourceNotFound,
                traceId),
            _ => throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}")
        };
    }

    private ObjectResult ProblemResponse(
        int status,
        string type,
        string title,
        string detail,
        string errorCode,
        string traceId)
    {
        var problem = new ProblemDetails
        {
            Type = $"https://edutwin.local/problems/{type}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errorCode"] = errorCode;
        return StatusCode(status, problem);
    }
}
