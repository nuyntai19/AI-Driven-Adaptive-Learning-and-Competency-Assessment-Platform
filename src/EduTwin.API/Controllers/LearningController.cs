using System.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.AssessmentAndReasoning.Polling;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/learning")]
[Authorize]
public sealed class LearningController : ControllerBase
{
    private readonly ISubmitAttemptUseCase _submitAttemptUseCase;
    private readonly IListAttemptsUseCase _listAttemptsUseCase;
    private readonly IGetAnalysisJobStatusUseCase _getAnalysisJobStatusUseCase;
    private readonly TimeProvider _timeProvider;

    public LearningController(
        ISubmitAttemptUseCase submitAttemptUseCase,
        IListAttemptsUseCase listAttemptsUseCase,
        IGetAnalysisJobStatusUseCase getAnalysisJobStatusUseCase,
        TimeProvider timeProvider)
    {
        _submitAttemptUseCase = submitAttemptUseCase;
        _listAttemptsUseCase = listAttemptsUseCase;
        _getAnalysisJobStatusUseCase = getAnalysisJobStatusUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet("attempts")]
    [ProducesResponseType(typeof(AttemptListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAttempts(
        [FromQuery] ListAttemptsQuery query,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _listAttemptsUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new AttemptListResponse
            {
                Data = result.Data ?? throw new InvalidOperationException(
                    "Attempt list succeeded without response data."),
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

        return MapAttemptListErrorToResponse(result.ErrorCode, traceId);
    }

    [HttpPost("attempts")]
    [Authorize(Policy = AuthorizationPolicies.StudentOnly)]
    [ProducesResponseType(typeof(SubmitAttemptResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitAttempt(
        [FromBody] SubmitAttemptRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _submitAttemptUseCase.ExecuteAsync(
            request,
            correlationId,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Accepted(new SubmitAttemptResponse
            {
                Data = result.Data ??
                    throw new InvalidOperationException("Submit attempt succeeded without response data."),
                Meta = new MetaDto
                {
                    TraceId = correlationId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return MapErrorToResponse(result.ErrorCode, correlationId);
    }

    [HttpGet("analysis-jobs/{analysisJobId}")]
    [ProducesResponseType(typeof(AnalysisJobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalysisJobStatus(
        [FromRoute] string analysisJobId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _getAnalysisJobStatusUseCase.ExecuteAsync(
            analysisJobId,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new AnalysisJobStatusResponse
            {
                Data = result.Data ?? throw new InvalidOperationException(
                    "Analysis job status succeeded without response data."),
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return MapAnalysisJobErrorToResponse(result.ErrorCode, traceId);
    }

    private IActionResult MapErrorToResponse(string? errorCode, string traceId) =>
        errorCode switch
        {
            ErrorCodes.ValidationFailed => BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/validation",
                "Dữ liệu không hợp lệ",
                "Một hoặc nhiều trường trong bài làm không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed)),

            ErrorCodes.ResourceNotFound => NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                "Không tìm thấy học sinh hoặc câu hỏi trong phạm vi trung tâm hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound)),

            ErrorCodes.DuplicateSubmission => Conflict(CreateProblemDetails(
                StatusCodes.Status409Conflict,
                "https://edutwin.local/problems/duplicate-submission",
                "Bài làm bị trùng",
                "Mã gửi bài đã được dùng cho một nội dung khác.",
                traceId,
                ErrorCodes.DuplicateSubmission)),

            ErrorCodes.AssignmentNotAvailable => UnprocessableEntity(CreateProblemDetails(
                StatusCodes.Status422UnprocessableEntity,
                "https://edutwin.local/problems/assignment-not-available",
                "Bài tập không khả dụng",
                "Bài tập không còn khả dụng hoặc học sinh không thuộc danh sách được giao.",
                traceId,
                ErrorCodes.AssignmentNotAvailable)),

            ErrorCodes.QuestionReasoningRequired => UnprocessableEntity(CreateProblemDetails(
                StatusCodes.Status422UnprocessableEntity,
                "https://edutwin.local/problems/reasoning-required",
                "Thiếu phần giải thích",
                "Câu hỏi này yêu cầu học sinh nhập phần giải thích trước khi gửi.",
                traceId,
                ErrorCodes.QuestionReasoningRequired)),

            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };

    private IActionResult MapAnalysisJobErrorToResponse(
        string? errorCode,
        string traceId) =>
        errorCode switch
        {
            ErrorCodes.ValidationFailed => BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/validation",
                "Dữ liệu không hợp lệ",
                "Mã công việc phân tích không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed)),

            ErrorCodes.ForbiddenResource => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    "Bạn không có quyền xem công việc phân tích này.",
                    traceId,
                    ErrorCodes.ForbiddenResource)),

            ErrorCodes.ResourceNotFound => NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                "Không tìm thấy công việc phân tích trong phạm vi trung tâm hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound)),

            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };

    private IActionResult MapAttemptListErrorToResponse(
        string? errorCode,
        string traceId) =>
        errorCode switch
        {
            ErrorCodes.ValidationFailed => BadRequest(CreateProblemDetails(
                StatusCodes.Status400BadRequest,
                "https://edutwin.local/problems/validation",
                "Dữ liệu không hợp lệ",
                "Một hoặc nhiều bộ lọc danh sách bài làm không hợp lệ.",
                traceId,
                ErrorCodes.ValidationFailed)),

            ErrorCodes.ForbiddenResource => StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "https://edutwin.local/problems/forbidden",
                    "Không có quyền truy cập",
                    "Bạn không có quyền xem danh sách bài làm của học sinh này.",
                    traceId,
                    ErrorCodes.ForbiddenResource)),

            ErrorCodes.ResourceNotFound => NotFound(CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "https://edutwin.local/problems/not-found",
                "Không tìm thấy dữ liệu",
                "Không tìm thấy học sinh hoặc bộ lọc trong phạm vi trung tâm hiện tại.",
                traceId,
                ErrorCodes.ResourceNotFound)),

            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };

    private ProblemDetails CreateProblemDetails(
        int status,
        string type,
        string title,
        string detail,
        string traceId,
        string errorCode) =>
        new()
        {
            Type = type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId,
                ["errorCode"] = errorCode
            }
        };
}
