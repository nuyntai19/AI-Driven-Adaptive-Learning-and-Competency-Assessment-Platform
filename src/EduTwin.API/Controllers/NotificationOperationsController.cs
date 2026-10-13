using EduTwin.API.Security;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EduTwin.API.Controllers;

[ApiController, Route("api/v1/notification-operations")]
public sealed class NotificationOperationsController(NotificationOperationsService service, TimeProvider clock) : ControllerBase
{
    [HttpGet, Authorize(Policy = CompositePermissionPolicies.NotificationOperationsRead)]
    public async Task<IActionResult> List(int page = 1, int pageSize = 20, string? state = null, CancellationToken ct = default)
    {
        var result = await service.ListAsync(page, pageSize, state, ct);
        return result.IsSuccess ? Ok(new { data = result.Data!.Items, summary = result.Data.Summary, meta = new PagedMetaDto { Page = page,
            PageSize = pageSize, TotalItems = result.Data.TotalItems, TotalPages = (int)Math.Ceiling(result.Data.TotalItems / (double)pageSize),
            Timestamp = clock.GetUtcNow().UtcDateTime, TraceId = HttpContext.TraceIdentifier } }) : Failure(result.ErrorCode!);
    }
    [HttpPost("{eventId:guid}/redrive"), Authorize(Policy = CompositePermissionPolicies.NotificationOperationsRedrive), EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> Redrive(Guid eventId, NotificationRedriveRequest request, CancellationToken ct)
    {
        var error = await service.RedriveAsync(eventId, request, ct);
        return error == null ? NoContent() : Failure(error);
    }
    private ObjectResult Failure(string error) => Problem(statusCode: error == ErrorCodes.ValidationFailed ? 400 : error == ErrorCodes.ConcurrencyConflict ? 409 : 404,
        title: "Không thể thực hiện thao tác vận hành thông báo", extensions: new Dictionary<string, object?> { ["errorCode"] = error });
}
