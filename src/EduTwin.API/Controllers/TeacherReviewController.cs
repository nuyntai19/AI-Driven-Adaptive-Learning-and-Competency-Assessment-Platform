using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.Assignments;
using EduTwin.BLL.DigitalTwin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/teachers/me")]
public sealed class TeacherReviewController : ControllerBase
{
    private readonly IListTeacherReviewQueueUseCase _reviewQueueUseCase;
    private readonly ITeacherOverrideUseCase? _overrideUseCase;
    private readonly ITeacherApproveUseCase? _approveUseCase;
    private readonly IApproveAssignmentResultUseCase? _approveAssignmentResultUseCase;
    private readonly IVoidAssignmentQuestionUseCase? _voidAssignmentQuestionUseCase;
    private readonly IGetTeacherStudentTwinUseCase? _teacherStudentTwinUseCase;
    private readonly TimeProvider _timeProvider;

    [ActivatorUtilitiesConstructor]
    public TeacherReviewController(
        IListTeacherReviewQueueUseCase reviewQueueUseCase,
        ITeacherOverrideUseCase? overrideUseCase,
        ITeacherApproveUseCase? approveUseCase,
        IApproveAssignmentResultUseCase? approveAssignmentResultUseCase,
        IGetTeacherStudentTwinUseCase teacherStudentTwinUseCase,
        TimeProvider timeProvider,
        IVoidAssignmentQuestionUseCase? voidAssignmentQuestionUseCase = null)
    {
        _reviewQueueUseCase = reviewQueueUseCase ?? throw new ArgumentNullException(nameof(reviewQueueUseCase));
        _overrideUseCase = overrideUseCase;
        _approveUseCase = approveUseCase;
        _approveAssignmentResultUseCase = approveAssignmentResultUseCase;
        _teacherStudentTwinUseCase = teacherStudentTwinUseCase;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _voidAssignmentQuestionUseCase = voidAssignmentQuestionUseCase;
    }

    public TeacherReviewController(
        IListTeacherReviewQueueUseCase reviewQueueUseCase,
        TimeProvider timeProvider)
        : this(reviewQueueUseCase, null, null, null, null!, timeProvider)
    {
    }

    public TeacherReviewController(
        IListTeacherReviewQueueUseCase reviewQueueUseCase,
        ITeacherOverrideUseCase? overrideUseCase,
        TimeProvider timeProvider)
        : this(reviewQueueUseCase, overrideUseCase, null, null, null!, timeProvider)
    {
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
        var result = await _reviewQueueUseCase.ExecuteAsync(query, cancellationToken);

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

    [HttpPost("reasoning-analyses/{analysisId}/override")]
    [Authorize(Policy = "twin.reasoning.override")]
    [ProducesResponseType(typeof(TeacherOverrideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OverrideAnalysis(
        [FromRoute] ulong analysisId,
        [FromBody] TeacherOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (_overrideUseCase is null)
        {
            throw new InvalidOperationException("TeacherOverrideUseCase is not configured.");
        }

        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _overrideUseCase.ExecuteAsync(analysisId, request, cancellationToken);

        if (result.Status == TeacherOverrideStatus.Success)
        {
            return Ok(new TeacherOverrideResponse
            {
                Data = result.Data ?? throw new InvalidOperationException(
                    "Override succeeded without response data."),
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return result.Status switch
        {
            TeacherOverrideStatus.ValidationFailed => ProblemResponse(
                StatusCodes.Status400BadRequest,
                "validation",
                "Dữ liệu không hợp lệ",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            TeacherOverrideStatus.Forbidden => ProblemResponse(
                StatusCodes.Status403Forbidden,
                "forbidden",
                "Không có quyền can thiệp",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            TeacherOverrideStatus.NotFound => ProblemResponse(
                StatusCodes.Status404NotFound,
                "not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            TeacherOverrideStatus.Conflict => ProblemResponse(
                StatusCodes.Status409Conflict,
                "conflict",
                "Xung đột phiên bản",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            _ => throw new InvalidOperationException($"Unexpected status: {result.Status}")
        };
    }

    [HttpPost("reasoning-analyses/{analysisId}/approve")]
    [Authorize(Policy = "twin.reasoning.override")]
    [ProducesResponseType(typeof(TeacherApproveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveAnalysis(
        [FromRoute] ulong analysisId,
        [FromBody] TeacherApproveRequest? request,
        CancellationToken cancellationToken)
    {
        if (_approveUseCase is null)
        {
            throw new InvalidOperationException("TeacherApproveUseCase is not configured.");
        }

        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _approveUseCase.ExecuteAsync(analysisId, request ?? new TeacherApproveRequest(), cancellationToken);

        if (result.Status == TeacherApproveStatus.Success)
        {
            return Ok(new TeacherApproveResponse
            {
                Data = result.Data ?? throw new InvalidOperationException("Approve succeeded without response data."),
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return result.Status switch
        {
            TeacherApproveStatus.ValidationFailed => ProblemResponse(
                StatusCodes.Status400BadRequest,
                "validation",
                "Dữ liệu không hợp lệ",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            TeacherApproveStatus.Forbidden => ProblemResponse(
                StatusCodes.Status403Forbidden,
                "forbidden",
                "Không có quyền phê duyệt",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            TeacherApproveStatus.NotFound => ProblemResponse(
                StatusCodes.Status404NotFound,
                "not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            TeacherApproveStatus.Conflict => ProblemResponse(
                StatusCodes.Status409Conflict,
                "conflict",
                "Xung đột phiên bản",
                result.ErrorMessage,
                result.ErrorCode,
                traceId),
            _ => throw new InvalidOperationException($"Unexpected status: {result.Status}")
        };
    }

    [HttpPost("assignments/{assignmentId:guid}/approve-result")]
    [Authorize(Policy = "twin.reasoning.override")]
    [ProducesResponseType(typeof(AssignmentFinalReviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAssignmentResult(
        [FromRoute] Guid assignmentId,
        [FromBody] ApproveAssignmentResultRequest request,
        CancellationToken cancellationToken)
    {
        if (_approveAssignmentResultUseCase is null)
            throw new InvalidOperationException("ApproveAssignmentResultUseCase is not configured.");

        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _approveAssignmentResultUseCase.ExecuteAsync(assignmentId, request, cancellationToken);
        if (result.Status == TeacherApproveStatus.Success)
        {
            return Ok(new AssignmentFinalReviewResponse
            {
                Data = result.Data!,
                Meta = new MetaDto { TraceId = traceId, Timestamp = _timeProvider.GetUtcNow().UtcDateTime }
            });
        }

        return result.Status switch
        {
            TeacherApproveStatus.ValidationFailed => ProblemResponse(400, "validation", "Chưa thể duyệt kết quả", result.ErrorMessage, result.ErrorCode, traceId),
            TeacherApproveStatus.Forbidden => ProblemResponse(403, "forbidden", "Không có quyền phê duyệt", result.ErrorMessage, result.ErrorCode, traceId),
            TeacherApproveStatus.NotFound => ProblemResponse(404, "not-found", "Không tìm thấy dữ liệu", result.ErrorMessage, result.ErrorCode, traceId),
            TeacherApproveStatus.Conflict => ProblemResponse(409, "conflict", "Xung đột phiên bản", result.ErrorMessage, result.ErrorCode, traceId),
            _ => throw new InvalidOperationException($"Unexpected status: {result.Status}")
        };
    }

    [HttpPost("assignments/{assignmentId:guid}/questions/{questionId}/void")]
    [Authorize(Policy = "twin.reasoning.override")]
    [ProducesResponseType(typeof(VoidAssignmentQuestionResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> VoidAssignmentQuestion(
        [FromRoute] Guid assignmentId,
        [FromRoute] ulong questionId,
        [FromBody] VoidAssignmentQuestionRequest request,
        CancellationToken cancellationToken)
    {
        if (_voidAssignmentQuestionUseCase is null)
            throw new InvalidOperationException("VoidAssignmentQuestionUseCase is not configured.");

        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _voidAssignmentQuestionUseCase.ExecuteAsync(assignmentId, questionId, request, cancellationToken);
        if (result.Status == VoidAssignmentQuestionStatus.Success)
        {
            return Ok(new
            {
                Data = result.Data!,
                Meta = new MetaDto { TraceId = traceId, Timestamp = _timeProvider.GetUtcNow().UtcDateTime }
            });
        }

        return result.Status switch
        {
            VoidAssignmentQuestionStatus.ValidationFailed => ProblemResponse(400, "validation", "Dữ liệu không hợp lệ", result.ErrorMessage, result.ErrorCode, traceId),
            VoidAssignmentQuestionStatus.Forbidden => ProblemResponse(403, "forbidden", "Không có quyền thao tác", result.ErrorMessage, result.ErrorCode, traceId),
            VoidAssignmentQuestionStatus.NotFound => ProblemResponse(404, "not-found", "Không tìm thấy dữ liệu", result.ErrorMessage, result.ErrorCode, traceId),
            VoidAssignmentQuestionStatus.Conflict => ProblemResponse(409, "conflict", "Xung đột dữ liệu", result.ErrorMessage, result.ErrorCode, traceId),
            _ => throw new InvalidOperationException($"Unexpected status: {result.Status}")
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

    [HttpGet("students/{studentId}/twin")]
    [Authorize(Policy = "twin.student.read_scoped")]
    [ProducesResponseType(typeof(StudentTwinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentTwin(
        [FromRoute] Guid studentId,
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (studentId == Guid.Empty)
        {
            return ProblemResponse(
                StatusCodes.Status400BadRequest,
                "bad-request",
                "Dữ liệu không hợp lệ",
                "Mã học viên (studentId) không hợp lệ.",
                ErrorCodes.ValidationFailed,
                traceId);
        }

        if (subjectId == Guid.Empty)
        {
            return ProblemResponse(
                StatusCodes.Status400BadRequest,
                "bad-request",
                "Dữ liệu không hợp lệ",
                "Mã môn học (subjectId) không hợp lệ.",
                ErrorCodes.ValidationFailed,
                traceId);
        }

        if (_teacherStudentTwinUseCase is null)
        {
            throw new InvalidOperationException("TeacherStudentTwinUseCase is not configured.");
        }

        var result = await _teacherStudentTwinUseCase.ExecuteAsync(studentId, subjectId, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new StudentTwinResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            });
        }

        return result.ErrorCode switch
        {
            ErrorCodes.ForbiddenResource => ProblemResponse(
                StatusCodes.Status403Forbidden,
                "forbidden",
                "Không có quyền truy cập",
                result.ErrorMessage ?? "Bạn không có quyền truy cập Digital Twin của học sinh này.",
                ErrorCodes.ForbiddenResource,
                traceId),

            ErrorCodes.ResourceNotFound => ProblemResponse(
                StatusCodes.Status404NotFound,
                "not-found",
                "Không tìm thấy dữ liệu",
                result.ErrorMessage ?? "Không tìm thấy thông tin Digital Twin.",
                ErrorCodes.ResourceNotFound,
                traceId),

            _ => ProblemResponse(
                StatusCodes.Status400BadRequest,
                "bad-request",
                "Dữ liệu không hợp lệ",
                result.ErrorMessage ?? "Yêu cầu không hợp lệ.",
                result.ErrorCode ?? ErrorCodes.ValidationFailed,
                traceId)
        };
    }
}
