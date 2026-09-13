using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.Platform;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/platform/audit-logs")]
[Authorize(Policy = "platform.audit.read")]
public class PlatformAuditController : ControllerBase
{
    private readonly IPlatformAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public PlatformAuditController(
        IPlatformAuditService auditService,
        TimeProvider timeProvider)
    {
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> ListAuditLogs(
        [FromQuery] PlatformAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _auditService.ListAuditLogsAsync(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapErrorToResponse(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new
        {
            data = result.Data,
            meta = new
            {
                traceId,
                timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetAuditLog(
        [FromRoute] ulong id,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _auditService.GetAuditLogByIdAsync(id, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapErrorToResponse(result.ErrorCode, result.ErrorMessage);
        }

        return Ok(new
        {
            data = result.Data,
            meta = new
            {
                traceId,
                timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    private IActionResult MapErrorToResponse(string? errorCode, string? detail)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var path = HttpContext.Request.Path.Value;

        return errorCode switch
        {
            ErrorCodes.ForbiddenResource => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://edutwin.local/problems/forbidden",
                Title = "Truy cập bị từ chối",
                Status = StatusCodes.Status403Forbidden,
                Detail = detail ?? "Bạn không có quyền thực hiện thao tác trên tài nguyên này.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            ErrorCodes.ResourceNotFound => NotFound(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/not-found",
                Title = "Không tìm thấy dữ liệu",
                Status = StatusCodes.Status404NotFound,
                Detail = detail ?? "Tài nguyên được yêu cầu không tồn tại.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            _ => BadRequest(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/validation",
                Title = "Dữ liệu không hợp lệ",
                Status = StatusCodes.Status400BadRequest,
                Detail = detail ?? "Dữ liệu yêu cầu không hợp lệ.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode ?? ErrorCodes.ValidationFailed }
            })
        };
    }
}
