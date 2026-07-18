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
    private readonly TimeProvider _timeProvider;

    public CentersController(
        IGetCenterProfileUseCase getCenterProfileUseCase,
        TimeProvider timeProvider)
    {
        _getCenterProfileUseCase = getCenterProfileUseCase;
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
}
