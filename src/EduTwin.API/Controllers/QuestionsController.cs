using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/questions")]
[Authorize]
public class QuestionsController : ControllerBase
{
    private readonly ICreateQuestionUseCase _createUseCase;
    private readonly IGetQuestionUseCase _getUseCase;
    private readonly IListQuestionsUseCase _listUseCase;
    private readonly IUpdateQuestionUseCase _updateUseCase;
    private readonly IActivateQuestionUseCase _activateUseCase;
    private readonly IArchiveQuestionUseCase _archiveUseCase;
    private readonly IDeleteQuestionUseCase _deleteUseCase;
    private readonly TimeProvider _timeProvider;

    public QuestionsController(
        ICreateQuestionUseCase createUseCase,
        IGetQuestionUseCase getUseCase,
        IListQuestionsUseCase listUseCase,
        IUpdateQuestionUseCase updateUseCase,
        IActivateQuestionUseCase activateUseCase,
        IArchiveQuestionUseCase archiveUseCase,
        IDeleteQuestionUseCase deleteUseCase,
        TimeProvider timeProvider)
    {
        _createUseCase = createUseCase;
        _getUseCase = getUseCase;
        _listUseCase = listUseCase;
        _updateUseCase = updateUseCase;
        _activateUseCase = activateUseCase;
        _archiveUseCase = archiveUseCase;
        _deleteUseCase = deleteUseCase;
        _timeProvider = timeProvider;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(QuestionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateQuestionRequest request, CancellationToken cancellationToken)
    {
        var result = await _createUseCase.ExecuteAsync(request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new QuestionResponse
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
        return MapError(result.ErrorCode!);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(QuestionListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] QuestionListQuery query, CancellationToken cancellationToken)
    {
        var result = await _listUseCase.ExecuteAsync(query, cancellationToken);
        if (result.IsSuccess)
        {
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize < 1 ? 20 : (query.PageSize > 100 ? 100 : query.PageSize);
            var totalPages = (int)((result.TotalItems + pageSize - 1) / pageSize);
            
            var meta = new PagedMetaDto
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = result.TotalItems,
                TotalPages = totalPages,
                TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime
            };
            
            return Ok(new QuestionListResponse { Data = result.Data ?? new List<QuestionDto>(), Meta = meta });
        }
        return MapError(result.ErrorCode!);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(QuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _getUseCase.ExecuteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new QuestionResponse
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
        return MapError(result.ErrorCode!);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(QuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromRoute] string id, [FromBody] UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        var result = await _updateUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new QuestionResponse
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
        return MapError(result.ErrorCode!);
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(QuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate([FromRoute] string id, [FromBody] ActivateQuestionRequest request, CancellationToken cancellationToken)
    {
        var result = await _activateUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new QuestionResponse
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
        return MapError(result.ErrorCode!);
    }

    [HttpPost("{id}/archive")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(QuestionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive([FromRoute] string id, [FromBody] ArchiveQuestionRequest request, CancellationToken cancellationToken)
    {
        var result = await _archiveUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new QuestionResponse
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
        return MapError(result.ErrorCode!);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _deleteUseCase.ExecuteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return MapError(result.ErrorCode!);
    }

    private IActionResult MapError(string errorCode)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var path = HttpContext.Request.Path;
        
        return errorCode switch
        {
            ErrorCodes.ResourceNotFound => NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = StatusCodes.Status404NotFound,
                Detail = "Dữ liệu liên quan không tồn tại hoặc bạn không có quyền truy cập.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            ErrorCodes.ValidationFailed => BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Dữ liệu không hợp lệ",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Dữ liệu gửi lên không đúng định dạng hoặc thiếu thông tin.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            ErrorCodes.ConcurrencyConflict => Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Xung đột dữ liệu",
                Status = StatusCodes.Status409Conflict,
                Detail = "Dữ liệu đã bị thay đổi bởi người khác, vui lòng thử lại.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            ErrorCodes.InvalidStateTransition => UnprocessableEntity(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2",
                Title = "Trạng thái không hợp lệ",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = "Không thể thực hiện hành động do sai trạng thái.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            ErrorCodes.ForbiddenResource => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3",
                Title = "Không có quyền",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Bạn không có quyền thực hiện hành động này.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
                Title = "Lỗi hệ thống",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Đã xảy ra lỗi không xác định.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            })
        };
    }
}

