using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.Platform;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Platform;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/platform/centers")]
[Authorize]
public class PlatformCentersController : ControllerBase
{
    private readonly IPlatformCenterService _platformCenterService;
    private readonly TimeProvider _timeProvider;

    public PlatformCentersController(
        IPlatformCenterService platformCenterService,
        TimeProvider timeProvider)
    {
        _platformCenterService = platformCenterService;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [Authorize(Policy = "platform.centers.read")]
    public async Task<IActionResult> ListCenters(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _platformCenterService.ListCentersAsync(
            page, pageSize, search, status, traceId, cancellationToken);

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

    [HttpPost]
    [Authorize(Policy = "platform.centers.manage")]
    public async Task<IActionResult> CreateCenter(
        [FromBody] CreatePlatformCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _platformCenterService.CreateCenterAsync(
            request, traceId, cancellationToken);

        if (!result.IsSuccess)
        {
            return MapErrorToResponse(result.ErrorCode, result.ErrorMessage);
        }

        return StatusCode(StatusCodes.Status201Created, new
        {
            data = result.Data,
            meta = new
            {
                traceId,
                timestamp = _timeProvider.GetUtcNow().UtcDateTime
            }
        });
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "platform.centers.manage")]
    public async Task<IActionResult> UpdateCenterStatus(
        [FromRoute] Guid id,
        [FromBody] UpdatePlatformCenterStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _platformCenterService.UpdateCenterStatusAsync(
            id, request, traceId, cancellationToken);

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

    [HttpPost("{centerId:guid}/managers/{managerUserId:guid}/reset-password")]
    [Authorize(Policy = "platform.managers.manage")]
    public async Task<IActionResult> ResetCenterManagerPassword(
        [FromRoute] Guid centerId,
        [FromRoute] Guid managerUserId,
        [FromBody] ResetCenterManagerPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _platformCenterService.ResetCenterManagerPasswordAsync(
            centerId, managerUserId, request, traceId, cancellationToken);

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
        var path = HttpContext.Request.Path;

        return errorCode switch
        {
            ErrorCodes.ForbiddenResource or ErrorCodes.AuthPrivilegeEscalation => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://edutwin.local/problems/forbidden",
                Title = "Không có quyền truy cập",
                Status = StatusCodes.Status403Forbidden,
                Detail = detail ?? "Bạn không có quyền thực hiện hành động này.",
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
            ErrorCodes.ConcurrencyConflict => Conflict(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/concurrency-conflict",
                Title = "Xung đột phiên bản",
                Status = StatusCodes.Status409Conflict,
                Detail = detail ?? "Dữ liệu đã được cập nhật bởi một phiên làm việc khác.",
                Instance = path,
                Extensions = { ["traceId"] = traceId, ["errorCode"] = errorCode }
            }),
            ErrorCodes.DuplicateResource => Conflict(new ProblemDetails
            {
                Type = "https://edutwin.local/problems/duplicate-resource",
                Title = "Tài nguyên đã tồn tại",
                Status = StatusCodes.Status409Conflict,
                Detail = detail ?? "Tài nguyên đã tồn tại trong hệ thống.",
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
