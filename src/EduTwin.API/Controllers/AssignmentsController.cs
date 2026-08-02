using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/assignments")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly ICreateAssignmentUseCase _createAssignmentUseCase;
    private readonly IGetAssignmentUseCase _getAssignmentUseCase;
    private readonly IListAssignmentsUseCase _listAssignmentsUseCase;
    private readonly IUpdateAssignmentUseCase _updateAssignmentUseCase;
    private readonly TimeProvider _timeProvider;

    public AssignmentsController(
        ICreateAssignmentUseCase createAssignmentUseCase,
        IGetAssignmentUseCase getAssignmentUseCase,
        IListAssignmentsUseCase listAssignmentsUseCase,
        IUpdateAssignmentUseCase updateAssignmentUseCase,
        TimeProvider timeProvider)
    {
        _createAssignmentUseCase = createAssignmentUseCase;
        _getAssignmentUseCase = getAssignmentUseCase;
        _listAssignmentsUseCase = listAssignmentsUseCase;
        _updateAssignmentUseCase = updateAssignmentUseCase;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// POST /api/v1/assignments — Tạo Assignment Draft mới (API_CONTRACTS.md §50).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] CreateAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createAssignmentUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new AssignmentResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Created(string.Empty, response);
        }

        return MapErrorToResponse(result.ErrorCode);
    }

    /// <summary>
    /// GET /api/v1/assignments — Danh sách Assignment (API_CONTRACTS.md §51).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(AssignmentListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAssignments(
        [FromQuery] ListAssignmentsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _listAssignmentsUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            var pageSize = query.PageSize < 1 ? 20 : query.PageSize > 100 ? 100 : query.PageSize;
            var page = query.Page < 1 ? 1 : query.Page;
            var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling((double)result.TotalItems / pageSize);

            var response = new AssignmentListResponse
            {
                Data = result.Data ?? new System.Collections.Generic.List<AssignmentDto>(),
                Meta = new PagedMetaDto
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = totalPages,
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return MapErrorToResponse(result.ErrorCode);
    }

    /// <summary>
    /// GET /api/v1/assignments/{id} — Chi tiết Assignment (API_CONTRACTS.md §51).
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _getAssignmentUseCase.ExecuteAsync(id, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new AssignmentResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return MapErrorToResponse(result.ErrorCode);
    }

    /// <summary>
    /// PATCH /api/v1/assignments/{id} — Cập nhật Assignment Draft (API_CONTRACTS.md §51).
    /// Chỉ cho phép khi Status == Draft. Bắt buộc rowVersion.
    /// </summary>
    [HttpPatch("{id}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAssignment(
        [FromRoute] Guid id,
        [FromBody] UpdateAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateAssignmentUseCase.ExecuteAsync(id, request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new AssignmentResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        return MapErrorToResponse(result.ErrorCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IActionResult MapErrorToResponse(string? errorCode)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        return errorCode switch
        {
            ErrorCodes.ResourceNotFound => NotFound(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/not-found",
                Title = "Không tìm thấy dữ liệu",
                Status = StatusCodes.Status404NotFound,
                Detail = "Dữ liệu liên quan không tồn tại hoặc bạn không có quyền truy cập.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ResourceNotFound }
            }),

            ErrorCodes.ValidationFailed => BadRequest(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/validation",
                Title = "Dữ liệu không hợp lệ",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Một hoặc nhiều trường không hợp lệ.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ValidationFailed }
            }),

            ErrorCodes.InvalidStateTransition => Conflict(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/invalid-state",
                Title = "Trạng thái không hợp lệ",
                Status = StatusCodes.Status409Conflict,
                Detail = "Chỉ được phép cập nhật Assignment ở trạng thái Draft.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.InvalidStateTransition }
            }),

            ErrorCodes.ConcurrencyConflict => Conflict(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/concurrency",
                Title = "Xung đột phiên bản",
                Status = StatusCodes.Status409Conflict,
                Detail = "rowVersion không khớp. Vui lòng tải lại dữ liệu mới nhất.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ConcurrencyConflict }
            }),

            ErrorCodes.ForbiddenResource => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://edutwin.local/problems/forbidden",
                Title = "Không có quyền truy cập",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Bạn không có quyền thực hiện thao tác này.",
                Instance = HttpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = ErrorCodes.ForbiddenResource }
            }),

            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };
    }
}
