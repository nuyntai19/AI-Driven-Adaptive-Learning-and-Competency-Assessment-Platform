using EduTwin.BLL.Platform;
using EduTwin.BLL.Platform.Operations;
using EduTwin.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/platform")]
[Authorize]
public sealed class PlatformOperationsController(IPlatformOperationsService service, TimeProvider clock) : ControllerBase
{
    [HttpGet("operations")]
    [Authorize(Policy = "platform.operations.read")]
    public async Task<IActionResult> Operations([FromQuery] Guid? centerId, CancellationToken ct) => Map(await service.OperationsAsync(centerId, ct));
    [HttpGet("ai-usage")]
    [Authorize(Policy = "platform.ai_usage.read")]
    public async Task<IActionResult> AiUsage([FromQuery] int days = 7, CancellationToken ct = default) => Map(await service.AiUsageAsync(days, ct));
    [HttpGet("storage")]
    [Authorize(Policy = "platform.storage.read")]
    public async Task<IActionResult> Storage(CancellationToken ct) => Map(await service.StorageAsync(ct));
    private IActionResult Map<T>(PlatformResult<T> result)
    {
        if (result.IsSuccess) return Ok(new { data = result.Data, meta = new { timestamp = clock.GetUtcNow(), traceId = HttpContext.TraceIdentifier } });
        var status = result.ErrorCode == ErrorCodes.ForbiddenResource ? 403 : 400;
        return StatusCode(status, new ProblemDetails { Status = status, Title = status == 403 ? "Không có quyền truy cập" : "Bộ lọc không hợp lệ",
            Extensions = { ["traceId"] = HttpContext.TraceIdentifier, ["errorCode"] = result.ErrorCode } });
    }
}
