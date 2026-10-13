using System.Diagnostics;
using EduTwin.API.Security;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationInboxService inbox, TimeProvider clock) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Policy = CompositePermissionPolicies.NotificationRead)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var result = await inbox.SummaryAsync(ct);
        return result.IsSuccess ? Ok(new { data = result.Data, meta = Meta() }) : Failure(result.ErrorCode!);
    }

    [HttpGet]
    [Authorize(Policy = CompositePermissionPolicies.NotificationRead)]
    public async Task<IActionResult> List([FromQuery] NotificationListQuery query, CancellationToken ct)
    {
        var result = await inbox.ListAsync(query, ct);
        return result.IsSuccess ? Ok(new { data = result.Data!.Items, meta = new PagedMetaDto {
            Page = query.Page, PageSize = query.PageSize, TotalItems = result.Data.TotalItems,
            TotalPages = (int)Math.Ceiling(result.Data.TotalItems / (double)query.PageSize),
            Timestamp = clock.GetUtcNow().UtcDateTime, TraceId = TraceId() } }) : Failure(result.ErrorCode!);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = CompositePermissionPolicies.NotificationRead)]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        var result = await inbox.DetailAsync(id, ct);
        return result.IsSuccess ? Ok(new { data = result.Data, meta = Meta() }) : Failure(result.ErrorCode!);
    }

    [HttpPost("{id}/read")]
    [Authorize(Policy = CompositePermissionPolicies.NotificationUpdate)]
    public async Task<IActionResult> Read(string id, CancellationToken ct)
    {
        var error = await inbox.ReadAsync([id], ct);
        return error is null ? NoContent() : Failure(error);
    }

    [HttpPost("read-batch")]
    [Authorize(Policy = CompositePermissionPolicies.NotificationUpdate)]
    public async Task<IActionResult> ReadBatch([FromBody] ReadNotificationBatchRequest request, CancellationToken ct)
    {
        var error = await inbox.ReadAsync(request.DeliveryIds, ct);
        return error is null ? NoContent() : Failure(error);
    }

    private string TraceId() => Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    [HttpPost("{id}/hide"), Authorize(Policy = CompositePermissionPolicies.NotificationUpdate), EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> Hide(string id, CancellationToken ct)
    {
        var error = await inbox.HideAsync(id, ct);
        return error is null ? NoContent() : Failure(error);
    }
    [HttpPost("{id}/acknowledge"), Authorize(Policy = CompositePermissionPolicies.NotificationAcknowledge), EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> Acknowledge(string id, CancellationToken ct)
    {
        var error = await inbox.AcknowledgeAsync(id, ct);
        return error is null ? NoContent() : Failure(error);
    }
    [HttpPost("{id}/reply"), Authorize(Policy = CompositePermissionPolicies.NotificationReply), EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> Reply(string id, [FromBody] ReplyNotificationRequest request, CancellationToken ct)
    {
        var result = await inbox.ReplyAsync(id, request, ct);
        return result.IsSuccess ? Ok(new { data = result.Data, meta = Meta() }) : Failure(result.ErrorCode!);
    }
    private object Meta() => new { timestamp = clock.GetUtcNow().UtcDateTime, traceId = TraceId() };
    private ObjectResult Failure(string error) => Problem(statusCode: error switch { ErrorCodes.ValidationFailed or NotificationContentPolicy.Restricted => 400, ErrorCodes.ConcurrencyConflict => 409, ErrorCodes.ForbiddenResource => 403, _ => 404 },
        title: error == NotificationContentPolicy.Restricted ? "Nội dung có dấu hiệu không được phép gửi qua kênh này" : error == ErrorCodes.ValidationFailed ? "Dữ liệu thông báo không hợp lệ" : "Không tìm thấy thông báo",
        type: "https://edutwin.local/problems/" + (error is ErrorCodes.ValidationFailed or NotificationContentPolicy.Restricted ? "validation" : "resource-not-found"),
        instance: HttpContext.Request.Path, extensions: new Dictionary<string, object?> { ["errorCode"] = error });
}
