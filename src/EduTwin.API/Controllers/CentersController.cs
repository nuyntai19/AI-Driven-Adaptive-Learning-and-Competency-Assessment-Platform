using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.Dashboards;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Dashboards;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/centers")]
[Authorize]
public class CentersController : ControllerBase
{
    private readonly IGetCenterProfileUseCase _getCenterProfileUseCase;
    private readonly IUpdateCenterProfileUseCase _updateCenterProfileUseCase;
    private readonly IGetCenterDashboardUseCase _getCenterDashboardUseCase;
    private readonly TimeProvider _timeProvider;

    public CentersController(
        IGetCenterProfileUseCase getCenterProfileUseCase,
        IUpdateCenterProfileUseCase updateCenterProfileUseCase,
        TimeProvider timeProvider)
        : this(getCenterProfileUseCase, updateCenterProfileUseCase, null!, timeProvider)
    {
    }

    [ActivatorUtilitiesConstructor]
    public CentersController(
        IGetCenterProfileUseCase getCenterProfileUseCase,
        IUpdateCenterProfileUseCase updateCenterProfileUseCase,
        IGetCenterDashboardUseCase getCenterDashboardUseCase,
        TimeProvider timeProvider)
    {
        _getCenterProfileUseCase = getCenterProfileUseCase;
        _updateCenterProfileUseCase = updateCenterProfileUseCase;
        _getCenterDashboardUseCase = getCenterDashboardUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet("me")]
    [Authorize(Policy = "organization.center.read")]
    public async Task<IActionResult> GetMyCenterProfile(CancellationToken cancellationToken)
    {
        var result = await _getCenterProfileUseCase.ExecuteAsync(cancellationToken);

        if (result.IsSuccess)
        {
            var response = new CenterProfileResponse
            {
                Data = new CenterProfileDataDto
                {
                    CenterId = result.CenterId!,
                    CenterCode = result.CenterCode!,
                    CenterName = result.CenterName!,
                    Status = result.Status!,
                    Timezone = result.Timezone!,
                    RowVersion = result.RowVersion!
                },
                Meta = new MetaDto
                {
                    TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = 404,
                Detail = "Tài khoản hoặc trung tâm không tồn tại.",
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

    [HttpPatch("me")]
    [Authorize(Policy = "organization.center.update")]
    public async Task<IActionResult> UpdateMyCenterProfile([FromBody] UpdateCenterProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _updateCenterProfileUseCase.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new CenterProfileResponse
            {
                Data = new CenterProfileDataDto
                {
                    CenterId = result.CenterId!,
                    CenterCode = result.CenterCode!,
                    CenterName = result.CenterName!,
                    Status = result.Status!,
                    Timezone = result.Timezone!,
                    RowVersion = result.RowVersion!
                },
                Meta = new MetaDto
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
                Detail = "Dữ liệu cập nhật không hợp lệ.",
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
                Detail = "Tài khoản hoặc trung tâm không tồn tại.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ResourceNotFound
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ConcurrencyConflict)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8",
                Title = "Xung đột dữ liệu",
                Status = 409,
                Detail = "Dữ liệu đã bị thay đổi bởi người khác, vui lòng thử lại.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    ["errorCode"] = ErrorCodes.ConcurrencyConflict
                }
            });
        }

        throw new InvalidOperationException($"Unexpected error code: {result.ErrorCode}");
    }

    [HttpGet("me/dashboard")]
    [Authorize(Policy = "dashboards.center.read")]
    public async Task<IActionResult> GetCenterDashboard(
        [FromQuery] Guid? subjectId,
        [FromQuery] decimal riskThreshold = 70m,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var result = await _getCenterDashboardUseCase.ExecuteAsync(subjectId, riskThreshold, cancellationToken);

        if (result.IsSuccess)
        {
            var response = new CenterDashboardResponse
            {
                Data = result.Data!,
                Meta = new MetaDto
                {
                    TraceId = traceId,
                    Timestamp = _timeProvider.GetUtcNow().UtcDateTime
                }
            };
            return Ok(response);
        }

        if (result.ErrorCode == ErrorCodes.ResourceNotFound)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4",
                Title = "Không tìm thấy dữ liệu",
                Status = StatusCodes.Status404NotFound,
                Detail = result.ErrorMessage ?? "Không tìm thấy trung tâm hoặc dữ liệu.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = traceId,
                    ["errorCode"] = ErrorCodes.ResourceNotFound
                }
            });
        }

        if (result.ErrorCode == ErrorCodes.ForbiddenResource)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3",
                Title = "Không có quyền truy cập",
                Status = StatusCodes.Status403Forbidden,
                Detail = result.ErrorMessage ?? "Bạn không có quyền truy cập trung tâm này.",
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = traceId,
                    ["errorCode"] = ErrorCodes.ForbiddenResource
                }
            });
        }

        return BadRequest(new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
            Title = "Dữ liệu không hợp lệ",
            Status = StatusCodes.Status400BadRequest,
            Detail = result.ErrorMessage ?? "Yêu cầu không hợp lệ.",
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId,
                ["errorCode"] = result.ErrorCode ?? ErrorCodes.ValidationFailed
            }
        });
    }
}
