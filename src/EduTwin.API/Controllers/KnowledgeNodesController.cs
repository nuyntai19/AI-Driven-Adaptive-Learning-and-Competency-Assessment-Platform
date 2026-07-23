using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/knowledge/nodes")]
[Authorize]
public class KnowledgeNodesController : ControllerBase
{
    private readonly IListKnowledgeNodesUseCase _listKnowledgeNodesUseCase;
    private readonly ICreateKnowledgeNodeUseCase _createKnowledgeNodeUseCase;
    private readonly IUpdateKnowledgeNodeUseCase _updateKnowledgeNodeUseCase;
    private readonly TimeProvider _timeProvider;

    public KnowledgeNodesController(
        IListKnowledgeNodesUseCase listKnowledgeNodesUseCase,
        ICreateKnowledgeNodeUseCase createKnowledgeNodeUseCase,
        IUpdateKnowledgeNodeUseCase updateKnowledgeNodeUseCase,
        TimeProvider timeProvider)
    {
        _listKnowledgeNodesUseCase = listKnowledgeNodesUseCase;
        _createKnowledgeNodeUseCase = createKnowledgeNodeUseCase;
        _updateKnowledgeNodeUseCase = updateKnowledgeNodeUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(KnowledgeNodeListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListKnowledgeNodes([FromQuery] KnowledgeNodeListQuery query, CancellationToken cancellationToken)
    {
        var result = await _listKnowledgeNodesUseCase.ExecuteAsync(query, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new KnowledgeNodeListResponse
            {
                Data = result.Data ?? new System.Collections.Generic.List<KnowledgeNodeDto>(),
                Meta = new EduTwin.Contracts.IdentityAndTenancy.MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Dữ liệu không hợp lệ",
                Status = 400,
                Detail = "Tham số truy vấn không hợp lệ hoặc không đúng định dạng.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ValidationFailed
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = 404,
                Detail = "Dữ liệu không tồn tại hoặc bạn không có quyền truy cập.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ResourceNotFound
                }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpPost]
    [Authorize(Policy = EduTwin.BLL.IdentityAndTenancy.AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(KnowledgeNodeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateKnowledgeNode([FromBody] CreateKnowledgeNodeRequest request, CancellationToken cancellationToken)
    {
        var result = await _createKnowledgeNodeUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new KnowledgeNodeResponse
            {
                Data = result.Data!,
                Meta = new EduTwin.Contracts.IdentityAndTenancy.MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Created(string.Empty, response);
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Dữ liệu không hợp lệ",
                Status = 400,
                Detail = "Dữ liệu gửi lên không đúng định dạng hoặc thiếu thông tin.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ValidationFailed
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = 404,
                Detail = "Dữ liệu liên quan không tồn tại hoặc bạn không có quyền truy cập.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ResourceNotFound
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.DuplicateResource)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Trùng lặp dữ liệu",
                Status = 409,
                Detail = "Mã node đã tồn tại trong môn học.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.DuplicateResource
                }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpPatch("{nodeId}")]
    [Authorize(Policy = EduTwin.BLL.IdentityAndTenancy.AuthorizationPolicies.TeacherOrCenterManager)]
    [ProducesResponseType(typeof(KnowledgeNodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateKnowledgeNode(
        string nodeId,
        [FromBody] UpdateKnowledgeNodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateKnowledgeNodeUseCase.ExecuteAsync(nodeId, request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new KnowledgeNodeResponse
            {
                Data = result.Data!,
                Meta = new EduTwin.Contracts.IdentityAndTenancy.MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ValidationFailed)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
                Title = "Dữ liệu không hợp lệ",
                Status = 400,
                Detail = "Dữ liệu gửi lên không đúng định dạng hoặc thiếu thông tin.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ValidationFailed
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = 404,
                Detail = "Dữ liệu không tồn tại hoặc bạn không có quyền truy cập.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ResourceNotFound
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ConcurrencyConflict || result.ErrorCode == ErrorCodes.DagCycleDetected)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Xung đột dữ liệu",
                Status = 409,
                Detail = "Phát hiện xung đột dữ liệu.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = result.ErrorCode
                }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }
}
