using System;
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
[Route("api/v1/curriculums")]
[Authorize]
public class CurriculumsController : ControllerBase
{
    private readonly ICreateCurriculumUseCase _createCurriculumUseCase;
    private readonly IListCurriculumsUseCase _listCurriculumsUseCase;
    private readonly IGetCurriculumUseCase _getCurriculumUseCase;
    private readonly IUpdateCurriculumUseCase _updateCurriculumUseCase;
    private readonly IAssignCurriculumClassesUseCase _assignCurriculumClassesUseCase;
    private readonly IAssignCurriculumNodesUseCase _assignCurriculumNodesUseCase;
    private readonly IPublishCurriculumUseCase _publishCurriculumUseCase;
    private readonly TimeProvider _timeProvider;

    public CurriculumsController(
        ICreateCurriculumUseCase createCurriculumUseCase,
        IListCurriculumsUseCase listCurriculumsUseCase,
        IGetCurriculumUseCase getCurriculumUseCase,
        IUpdateCurriculumUseCase updateCurriculumUseCase,
        IAssignCurriculumClassesUseCase assignCurriculumClassesUseCase,
        IAssignCurriculumNodesUseCase assignCurriculumNodesUseCase,
        IPublishCurriculumUseCase publishCurriculumUseCase,
        TimeProvider timeProvider)
    {
        _createCurriculumUseCase = createCurriculumUseCase;
        _listCurriculumsUseCase = listCurriculumsUseCase;
        _getCurriculumUseCase = getCurriculumUseCase;
        _updateCurriculumUseCase = updateCurriculumUseCase;
        _assignCurriculumClassesUseCase = assignCurriculumClassesUseCase;
        _assignCurriculumNodesUseCase = assignCurriculumNodesUseCase;
        _publishCurriculumUseCase = publishCurriculumUseCase;
        _timeProvider = timeProvider;
    }

    [HttpPost]
    [Authorize(Policy = "curriculum.curriculums.create")]
    [ProducesResponseType(typeof(CurriculumResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCurriculum(
        [FromBody] CreateCurriculumRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createCurriculumUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new CurriculumResponse
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
    [Authorize(Policy = "curriculum.curriculums.read")]
    [ProducesResponseType(typeof(CurriculumListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListCurriculums(
        [FromQuery] CurriculumListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _listCurriculumsUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new CurriculumListResponse
            {
                Data = result.Data ?? new List<CurriculumDto>(),
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

    [HttpGet("{id}")]
    [Authorize(Policy = "curriculum.curriculums.read")]
    [ProducesResponseType(typeof(CurriculumResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurriculum(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _getCurriculumUseCase.ExecuteAsync(new GetCurriculumRequest { CurriculumId = id }, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new CurriculumResponse
            {
                Data = result.Data!,
                Meta = new MetaDto { TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, Timestamp = _timeProvider.GetUtcNow().UtcDateTime }
            };
            return Ok(response);
        }
        return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = "Not found." });
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "curriculum.curriculums.update")]
    [ProducesResponseType(typeof(CurriculumResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCurriculum(
        [FromRoute] Guid id,
        [FromBody] UpdateCurriculumRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateCurriculumUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new CurriculumResponse
            {
                Data = result.Data!,
                Meta = new MetaDto { TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, Timestamp = _timeProvider.GetUtcNow().UtcDateTime }
            };
            return Ok(response);
        }
        return MapError(result.ErrorCode!);
    }

    [HttpPut("{id}/classes")]
    [Authorize(Policy = "curriculum.curriculums.update")]
    [ProducesResponseType(typeof(CurriculumResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCurriculumClasses(
        [FromRoute] Guid id,
        [FromBody] AssignCurriculumClassesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assignCurriculumClassesUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new CurriculumResponse
            {
                Data = result.Data!,
                Meta = new MetaDto { TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, Timestamp = _timeProvider.GetUtcNow().UtcDateTime }
            };
            return Ok(response);
        }
        return MapError(result.ErrorCode!);
    }

    [HttpPut("{id}/nodes")]
    [Authorize(Policy = "curriculum.curriculums.update")]
    [ProducesResponseType(typeof(CurriculumResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignCurriculumNodes(
        [FromRoute] Guid id,
        [FromBody] AssignCurriculumNodesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _assignCurriculumNodesUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new CurriculumResponse
            {
                Data = result.Data!,
                Meta = new MetaDto { TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, Timestamp = _timeProvider.GetUtcNow().UtcDateTime }
            };
            return Ok(response);
        }
        return MapError(result.ErrorCode!);
    }

    [HttpPost("{id}/publish")]
    [Authorize(Policy = "curriculum.curriculums.publish")]
    [ProducesResponseType(typeof(CurriculumResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishCurriculum(
        [FromRoute] Guid id,
        [FromBody] PublishCurriculumRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _publishCurriculumUseCase.ExecuteAsync(id, request, cancellationToken);
        if (result.IsSuccess)
        {
            var response = new CurriculumResponse
            {
                Data = result.Data!,
                Meta = new MetaDto { TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, Timestamp = _timeProvider.GetUtcNow().UtcDateTime }
            };
            return Ok(response);
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
            ErrorCodes.InvalidStateTransition => Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Trạng thái không hợp lệ",
                Status = StatusCodes.Status409Conflict,
                Detail = "Không thể thực hiện hành động do sai trạng thái.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            _ => throw new InvalidOperationException($"Unexpected error code: {errorCode}")
        };
    }
}
