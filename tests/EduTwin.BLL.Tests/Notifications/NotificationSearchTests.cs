using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public class NotificationSearchTests
{
    [Fact]
    public async Task Search_MatchesTitleOrBodyBeforePagination_CombinesFilters_AndStaysInOwnInbox()
    {
        await using var f = await NotificationManualTests.Fixture.Create();
        var teacher = f.User(UserRole.Teacher);
        var manager = f.User(UserRole.CenterManager);
        await using var db = f.Context(teacher);
        var service = f.Service(db, teacher);
        async Task<NotificationSentDto> Send(string title, string body, string category = "Information", string priority = "Normal", bool ack = false)
        {
            var draft = NotificationManualTests.Draft("CenterManagers", manager.UserId);
            draft.Title = title; draft.Body = body; draft.Category = category; draft.Priority = priority; draft.RequiresAcknowledgement = ack;
            var preview = await service.PreviewAsync(draft, default);
            Assert.True(preview.IsSuccess);
            var sent = await service.SendAsync(NotificationManualTests.SendRequest(draft, preview.Data!.Token), default);
            Assert.True(sent.IsSuccess);
            var ev = await db.NotificationEvents.SingleAsync(e => e.MessageId == sent.Data!.MessageId);
            Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(ev.EventId));
            return sent.Data!;
        }
        var request = await Send("Họp phụ huynh", "Vui lòng phản hồi trước thứ hai.", "Request", "Important", true);
        await Send("Lịch tuần mới", "Mời tham dự họp phụ huynh.");
        await Send("Không liên quan", "Thông báo khác.");
        await using var rd = f.Context(manager);
        var inbox = new NotificationInboxService(rd, f.Tenant(manager), f.Clock);
        var search = await inbox.ListAsync(new() { Search = "  HỌP PHỤ HUYNH  ", PageSize = 1 }, default);
        Assert.Equal(2, search.Data!.TotalItems);
        Assert.Single(search.Data.Items);
        Assert.Single((await inbox.ListAsync(new() { Search = "họp phụ huynh", PageSize = 1, Page = 2 }, default)).Data!.Items);
        var combined = await inbox.ListAsync(new() { Search = "họp phụ huynh", Category = "Request", Priority = "Important",
            UnreadOnly = true, NeedsAcknowledgement = true, From = f.Clock.GetUtcNow().UtcDateTime.AddDays(-1), Until = f.Clock.GetUtcNow().UtcDateTime.AddDays(1) }, default);
        Assert.Equal("Họp phụ huynh", Assert.Single(combined.Data!.Items).Title);
        Assert.Empty((await inbox.ListAsync(new() { Search = "họp phụ huynh", Priority = "Urgent" }, default)).Data!.Items);
        Assert.Empty((await inbox.ListAsync(new() { Search = "họp phụ huynh", Until = f.Clock.GetUtcNow().UtcDateTime.AddDays(-1) }, default)).Data!.Items);
        Assert.Equal(3, (await inbox.ListAsync(new() { Search = "   " }, default)).Data!.TotalItems);
        Assert.Equal(ErrorCodes.ValidationFailed, (await inbox.ListAsync(new() { Search = new string('a', 201) }, default)).ErrorCode);
        var senderInbox = new NotificationInboxService(db, f.Tenant(teacher), f.Clock);
        Assert.Empty((await senderInbox.ListAsync(new() { Search = "họp phụ huynh" }, default)).Data!.Items);

        // The original title/body must not remain searchable after retraction.
        Assert.Null(await service.RetractAsync(request.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Nội dung thay đổi" }, default));
        Assert.Equal(1, (await inbox.ListAsync(new() { Search = "họp phụ huynh" }, default)).Data!.TotalItems);
        Assert.Equal("Thông báo đã được thu hồi", Assert.Single((await inbox.ListAsync(new() { Search = "thu hồi" }, default)).Data!.Items).Title);
        Assert.Single((await inbox.ListAsync(new() { Search = "nội dung thay đổi" }, default)).Data!.Items);
    }
}
