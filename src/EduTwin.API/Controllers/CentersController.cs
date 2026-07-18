using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/centers")]
[Authorize(Policy = AuthorizationPolicies.CenterManagerOnly)]
public class CentersController : ControllerBase
{
    private readonly IGetCenterProfileUseCase _getCenterProfileUseCase;
    private readonly IUpdateCenterProfileUseCase _updateCenterProfileUseCase;
    private readonly TimeProvider _timeProvider;

    public CentersController(
        IGetCenterProfileUseCase getCenterProfileUseCase,
        IUpdateCenterProfileUseCase updateCenterProfileUseCase,
        TimeProvider timeProvider)
    {
        _getCenterProfileUseCase = getCenterProfileUseCase;
        _updateCenterProfileUseCase = updateCenterProfileUseCase;
        _timeProvider = timeProvider;
    }

    [HttpGet("me")]
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
}
