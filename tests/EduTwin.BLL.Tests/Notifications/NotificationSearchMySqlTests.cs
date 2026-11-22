using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.API.Notifications;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed partial class NotificationMySqlTests
{
    [NotificationMySqlFact]
    public async Task SearchTitleBodyAndTombstone_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.CenterManager);
        await using var db = f.Context();
        var target = await db.Users.Where(u => u.UserId != f.UserId).Select(u => u.UserId).SingleAsync();
        var service = new NotificationManualService(db, f.Tenant, TimeProvider.System, new(db),
            new NotificationPreviewProtector(new EphemeralDataProtectionProvider()));
        var draft = NotificationManualTests.Draft("CenterTeachers", target);
        draft.Title = "Lịch họp đặc biệt";
        draft.Body = "Vui lòng tham dự.";
        draft.Category = "Request"; draft.Priority = "Important";
        var sent = (await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default)).Data!;
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync((await db.NotificationEvents.SingleAsync()).EventId));
        var recipient = new TenantContext(); recipient.Initialize(f.CenterId, target, "Teacher", 1);
        await using var rd = f.Context(recipient);
        var inbox = new NotificationInboxService(rd, recipient, TimeProvider.System);
        Assert.Single((await inbox.ListAsync(new() { Search = "  LỊCH HỌP  ", Category = "Request", Priority = "Important" }, default)).Data!.Items);
        Assert.Single((await inbox.ListAsync(new() { Search = "tham dự" }, default)).Data!.Items);
        Assert.Null(await service.RetractAsync(sent.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Lịch thay đổi" }, default));
        Assert.Empty((await inbox.ListAsync(new() { Search = "đặc biệt" }, default)).Data!.Items);
        Assert.Single((await inbox.ListAsync(new() { Search = "thu hồi" }, default)).Data!.Items);
        Assert.Single((await inbox.ListAsync(new() { Search = "lịch thay đổi" }, default)).Data!.Items);
    }
}
