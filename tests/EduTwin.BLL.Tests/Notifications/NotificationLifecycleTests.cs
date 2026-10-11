using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.Contracts.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed class NotificationLifecycleTests
{
    [Fact]
    public async Task AcknowledgeAndHide_AreIndependentAndIdempotent_OwnOnly()
    {
        await using var f = await NotificationManualTests.Fixture.Create();
        var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        var draft = NotificationManualTests.Draft("SelectedStudents", student.UserId); draft.RequiresAcknowledgement = true;
        var sent = await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default);
        await using var rd = f.Context(student); var inbox = new NotificationInboxService(rd, f.Tenant(student), f.Clock);
        Assert.True(await new NotificationDispatcher(rd, f.Clock).DispatchAsync((await rd.NotificationEvents.SingleAsync()).EventId));
        var id = (await inbox.ListAsync(new(), default)).Data!.Items.Single().DeliveryId;
        Assert.Null(await inbox.AcknowledgeAsync(id, default));
        var first = (await inbox.DetailAsync(id, default)).Data!;
        Assert.Null(first.ReadAt); Assert.NotNull(first.AcknowledgedAt);
        f.Clock.Now = f.Clock.Now.AddHours(1); Assert.Null(await inbox.AcknowledgeAsync(id, default));
        Assert.Equal(first.AcknowledgedAt, (await inbox.DetailAsync(id, default)).Data!.AcknowledgedAt);
        Assert.Equal(1, (await inbox.SummaryAsync(default)).Data!.UnreadCount);
        Assert.Equal(1, (await service.SentDetailAsync(sent.Data!.MessageId, default)).Data!.AcknowledgedCount);
        Assert.Equal(ErrorCodes.ResourceNotFound, await new NotificationInboxService(db, f.Tenant(teacher), f.Clock).AcknowledgeAsync(id, default));
        Assert.Null(await inbox.HideAsync(id, default)); Assert.Null(await inbox.HideAsync(id, default));
        Assert.Empty((await inbox.ListAsync(new(), default)).Data!.Items); Assert.Equal(0, (await inbox.SummaryAsync(default)).Data!.UnreadCount);
        Assert.True((await inbox.DetailAsync(id, default)).IsSuccess); Assert.Single(await rd.NotificationDeliveries.ToListAsync());
    }

    [Fact]
    public async Task Retraction_PreservesOriginal_TombstonesRecipient_AndSuppressesPending()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        var draft = NotificationManualTests.Draft("SelectedStudents", student.UserId); draft.RequiresAcknowledgement = true;
        var sent = await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default);
        await using var rd = f.Context(student); var ev = await rd.NotificationEvents.SingleAsync();
        Assert.True(await new NotificationDispatcher(rd, f.Clock).DispatchAsync(ev.EventId));
        var inbox = new NotificationInboxService(rd, f.Tenant(student), f.Clock); var id = (await inbox.ListAsync(new(), default)).Data!.Items.Single().DeliveryId;
        var request = new RetractNotificationRequest { ClientActionId = Guid.NewGuid(), Reason = "Sai lịch học" };
        Assert.Null(await service.RetractAsync(sent.Data!.MessageId, request, default)); Assert.Null(await service.RetractAsync(sent.Data.MessageId, request, default));
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await service.RetractAsync(sent.Data.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Khác" }, default));
        var item = (await inbox.DetailAsync(id, default)).Data!; Assert.Equal("Retracted", item.LifecycleStatus);
        Assert.DoesNotContain(draft.Body, item.Body); Assert.False(item.RequiresAcknowledgement);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await inbox.AcknowledgeAsync(id, default));
        Assert.Equal(draft.Body, (await service.SentDetailAsync(sent.Data.MessageId, default)).Data!.Body);
        Assert.Single(await db.NotificationMessageActions.ToListAsync());
        var second = await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default);
        Assert.Null(await service.RetractAsync(second.Data!.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Dừng gửi" }, default));
        var pending = await rd.NotificationEvents.SingleAsync(e => e.MessageId == second.Data.MessageId);
        Assert.True(await new NotificationDispatcher(rd, f.Clock).DispatchAsync(pending.EventId));
        Assert.Equal(1, await rd.NotificationDeliveries.CountAsync());
    }

    [Fact]
    public async Task Correction_IsNewImmutableMessage_AndNeverExpandsOriginalAudience()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        var draft = NotificationManualTests.Draft("SelectedStudents", student.UserId);
        var original = (await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default)).Data!;
        draft.CorrectionOfMessageId = original.MessageId; draft.CorrectionReason = "Sửa lịch"; draft.Body = "Nội dung mới";
        var unrelated = NotificationManualTests.Draft("CenterManagers", f.User(UserRole.CenterManager).UserId); unrelated.CorrectionOfMessageId = original.MessageId; unrelated.CorrectionReason = "Sai phạm vi";
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await service.PreviewAsync(unrelated, default)).ErrorCode);
        var correction = await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default);
        Assert.True(correction.IsSuccess); Assert.NotEqual(original.MessageId, correction.Data!.MessageId); Assert.Equal(original.MessageId, correction.Data.CorrectionOfMessageId);
        var old = (await service.SentDetailAsync(original.MessageId, default)).Data!; Assert.Equal("Corrected", old.LifecycleStatus); Assert.Equal(correction.Data.MessageId, old.ReplacementMessageId);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await service.PreviewAsync(draft, default)).ErrorCode);
        await using var foreign = f.Context(f.ForeignTeacher);
        Assert.Equal(ErrorCodes.ResourceNotFound, await f.Service(foreign, f.ForeignTeacher).RetractAsync(original.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Ngoài quyền" }, default));
    }

    [Fact]
    public async Task Reply_IsOnePrivateResponse_AndRevalidatesClassScope()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        var draft = NotificationManualTests.Draft("ClassStudents", f.ClassId); draft.AllowReply = true;
        var sent = (await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default)).Data!;
        await using var rd = f.Context(student); Assert.True(await new NotificationDispatcher(rd, f.Clock).DispatchAsync((await rd.NotificationEvents.SingleAsync()).EventId));
        var inbox = new NotificationInboxService(rd, f.Tenant(student), f.Clock); var id = (await inbox.ListAsync(new(), default)).Data!.Items.Single().DeliveryId;
        var request = new ReplyNotificationRequest { ClientReplyId = Guid.NewGuid(), Body = "Tôi đã chuẩn bị" };
        Assert.True((await inbox.ReplyAsync(id, request, default)).IsSuccess); Assert.True((await inbox.ReplyAsync(id, request, default)).IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await inbox.ReplyAsync(id, new() { ClientReplyId = Guid.NewGuid(), Body = "Lần hai" }, default)).ErrorCode);
        Assert.Equal(request.Body, (await inbox.DetailAsync(id, default)).Data!.OwnReply);
        Assert.Single((await service.RepliesAsync(sent.MessageId, new(), default)).Data!.Items);
        await using var foreign = f.Context(f.ForeignTeacher); Assert.Equal(ErrorCodes.ResourceNotFound, (await f.Service(foreign, f.ForeignTeacher).RepliesAsync(sent.MessageId, new(), default)).ErrorCode);
        var second = (await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default)).Data!;
        Assert.True(await new NotificationDispatcher(rd, f.Clock).DispatchAsync((await rd.NotificationEvents.SingleAsync(e => e.MessageId == second.MessageId)).EventId));
        (await db.Classes.SingleAsync()).Status = ClassStatus.Archived; await db.SaveChangesAsync();
        var secondId = (await inbox.ListAsync(new(), default)).Data!.Items.First().DeliveryId;
        Assert.Equal(ErrorCodes.ForbiddenResource, (await inbox.ReplyAsync(secondId, new() { ClientReplyId = Guid.NewGuid(), Body = "Lớp đã đóng" }, default)).ErrorCode);
    }

    [Fact]
    public async Task PriorityAndReceiptOptions_AreBoundToPreview_AndFiltersValidated()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        var draft = NotificationManualTests.Draft("CenterManagers", f.User(UserRole.CenterManager).UserId);
        var token = (await service.PreviewAsync(draft, default)).Data!.Token;
        var request = NotificationManualTests.SendRequest(draft, token); request.RequiresAcknowledgement = true;
        Assert.Equal(ErrorCodes.ValidationFailed, (await service.SendAsync(request, default)).ErrorCode);
        request = NotificationManualTests.SendRequest(draft, token); request.Priority = "Urgent";
        Assert.Equal(ErrorCodes.ValidationFailed, (await service.SendAsync(request, default)).ErrorCode);
        draft.Priority = "Invalid"; Assert.Equal(ErrorCodes.ValidationFailed, (await service.PreviewAsync(draft, default)).ErrorCode);
        var inbox = new NotificationInboxService(db, f.Tenant(teacher), f.Clock);
        Assert.Equal(ErrorCodes.ValidationFailed, (await inbox.ListAsync(new() { Category = "Unknown" }, default)).ErrorCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await inbox.ListAsync(new() { From = DateTime.UtcNow.AddDays(1), Until = DateTime.UtcNow }, default)).ErrorCode);
    }
}
