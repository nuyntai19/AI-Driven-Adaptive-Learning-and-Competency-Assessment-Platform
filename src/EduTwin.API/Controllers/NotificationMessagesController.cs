using System.Diagnostics;
using EduTwin.API.Security;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EduTwin.API.Controllers;

[ApiController, Route("api/v1/notifications"), Authorize]
public sealed class NotificationMessagesController(NotificationManualService service, TimeProvider clock, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("compose-options"), Authorize(Policy = CompositePermissionPolicies.NotificationSend)]
    public async Task<IActionResult> Options(CancellationToken ct) => Result(await service.OptionsAsync(ct));
    [HttpPost("preview"), Authorize(Policy = CompositePermissionPolicies.NotificationSend), EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> Preview([FromBody] NotificationComposeRequest request, CancellationToken ct)
    {
        if (request.CorrectionOfMessageId is not null && !(await authorization.AuthorizeAsync(User, null, CompositePermissionPolicies.NotificationRetract)).Succeeded) return Forbid();
        return Result(await service.PreviewAsync(request, ct));
    }
    [HttpPost("send"), Authorize(Policy = CompositePermissionPolicies.NotificationSend), EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> Send([FromBody] NotificationSendRequest request, CancellationToken ct)
    {
        if (request.CorrectionOfMessageId is not null && !(await authorization.AuthorizeAsync(User, null, CompositePermissionPolicies.NotificationRetract)).Succeeded) return Forbid();
        return Result(await service.SendAsync(request, ct));
    }
    [HttpPost("sent/{id:guid}/retract"), Authorize(Policy = CompositePermissionPolicies.NotificationRetract), EnableRateLimiting("PlatformSecurityPolicy")]
    public async Task<IActionResult> Retract(Guid id, [FromBody] RetractNotificationRequest request, CancellationToken ct)
    {
        var error = await service.RetractAsync(id, request, ct);
        return error is null ? NoContent() : Failure(error);
    }
    [HttpGet("sent/{id:guid}/replies"), Authorize(Policy = CompositePermissionPolicies.NotificationSentRead)]
    public async Task<IActionResult> Replies(Guid id, [FromQuery] NotificationListQuery query, CancellationToken ct)
    {
        var result = await service.RepliesAsync(id, query, ct);
        return result.IsSuccess ? Ok(new { data = result.Data!.Items, meta = new PagedMetaDto { Page = query.Page, PageSize = query.PageSize,
            TotalItems = result.Data.TotalItems, TotalPages = (int)Math.Ceiling(result.Data.TotalItems / (double)query.PageSize), Timestamp = clock.GetUtcNow().UtcDateTime, TraceId = TraceId() } }) : Failure(result.ErrorCode!);
    }
    [HttpGet("sent"), Authorize(Policy = CompositePermissionPolicies.NotificationSentRead)]
    public async Task<IActionResult> Sent([FromQuery] NotificationListQuery query, CancellationToken ct)
    {
        var result = await service.SentAsync(query, ct);
        return result.IsSuccess ? Ok(new { data = result.Data!.Items, meta = new PagedMetaDto { Page = query.Page, PageSize = query.PageSize,
            TotalItems = result.Data.TotalItems, TotalPages = (int)Math.Ceiling(result.Data.TotalItems / (double)query.PageSize),
            Timestamp = clock.GetUtcNow().UtcDateTime, TraceId = TraceId() } }) : Failure(result.ErrorCode!);
    }
    [HttpGet("sent/{id:guid}"), Authorize(Policy = CompositePermissionPolicies.NotificationSentRead)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Result(await service.SentDetailAsync(id, ct));
    private IActionResult Result<T>(NotificationResult<T> result) => result.IsSuccess ? Ok(new { data = result.Data,
        meta = new { timestamp = clock.GetUtcNow().UtcDateTime, traceId = TraceId() } }) : Failure(result.ErrorCode!);
    private string TraceId() => Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    private ObjectResult Failure(string error)
    {
        var status = error switch { ErrorCodes.ValidationFailed or NotificationContentPolicy.Restricted => 400, ErrorCodes.ForbiddenResource => 403,
            NotificationManualService.ScopeChanged or ErrorCodes.ConcurrencyConflict => 409, _ => 404 };
        return Problem(statusCode: status, title: error == NotificationManualService.ScopeChanged ? "Phạm vi người nhận đã thay đổi. Hãy xem trước lại." : "Không thể thực hiện thao tác thông báo",
            extensions: new Dictionary<string, object?> { ["errorCode"] = error });
    }
}
