using System.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning;
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
    private readonly TimeProvider _timeProvider;

    public LearningController(
        ISubmitAttemptUseCase submitAttemptUseCase,
        TimeProvider timeProvider)
    {
        _submitAttemptUseCase = submitAttemptUseCase;
        _timeProvider = timeProvider;
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
