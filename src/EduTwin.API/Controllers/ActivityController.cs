using EduTwin.API.Security;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController, Route("api/v1/activity"), Authorize]
public sealed class ActivityController(ActivityService service, TimeProvider clock) : ControllerBase
{
    [HttpGet, Authorize(Policy = CompositePermissionPolicies.ActivityRead)]
    public async Task<IActionResult> List([FromQuery] ActivityQuery query, CancellationToken ct)
    {
        var result = await service.ListAsync(query, ct);
        return result.IsSuccess ? Ok(new { data = result.Data!.Items, meta = new PagedMetaDto { Page = query.Page, PageSize = query.PageSize,
            TotalItems = result.Data.TotalItems, TotalPages = (int)Math.Ceiling(result.Data.TotalItems / (double)query.PageSize),
            Timestamp = clock.GetUtcNow().UtcDateTime, TraceId = HttpContext.TraceIdentifier } }) : Failure(result.ErrorCode!);
    }
    [HttpGet("{source}/{id}"), Authorize(Policy = CompositePermissionPolicies.ActivityRead)]
    public async Task<IActionResult> Detail(string source, string id, CancellationToken ct)
    {
        var result = await service.DetailAsync(source, id, ct);
        return result.IsSuccess ? Ok(new { data = result.Data, meta = new { timestamp = clock.GetUtcNow().UtcDateTime, traceId = HttpContext.TraceIdentifier } }) : Failure(result.ErrorCode!);
    }
    private ObjectResult Failure(string error) => Problem(statusCode: error == ErrorCodes.ValidationFailed ? 400 : 404,
        title: error == ErrorCodes.ValidationFailed ? "Bộ lọc nhật ký không hợp lệ" : "Không tìm thấy hoạt động", extensions: new Dictionary<string, object?> { ["errorCode"] = error });
}
