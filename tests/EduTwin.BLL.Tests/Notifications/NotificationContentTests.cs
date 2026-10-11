using EduTwin.BLL.Notifications;
using EduTwin.API.Controllers;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed class NotificationContentTests
{
    [Fact]
    public async Task MvcPreview_ContentRejectionKeeps400AndOneFactoryTraceId_Not500()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var manager = f.User(UserRole.CenterManager);
        await using var db = f.Context(manager);
        var services = new ServiceCollection(); services.AddLogging(); services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var controller = new NotificationMessagesController(f.Service(db, manager), f.Clock, Mock.Of<IAuthorizationService>())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "mvc-notification-regression" } } };
        var draft = NotificationManualTests.Draft("PlatformSupport", f.User(UserRole.PlatformAdmin).UserId); draft.Body = "Đáp án: thông tin synthetic";
        var result = Assert.IsType<ObjectResult>(await controller.Preview(draft, default));
        Assert.Equal(400, result.StatusCode); var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(NotificationContentPolicy.Restricted, problem.Extensions["errorCode"]);
        Assert.True(problem.Extensions.ContainsKey("traceId")); Assert.DoesNotContain("synthetic", problem.Title!);
        Assert.Empty(await db.NotificationMessages.ToArrayAsync());
    }

    [Theory]
    [InlineData(false, 404, ErrorCodes.ResourceNotFound)]
    [InlineData(true, 400, ErrorCodes.ValidationFailed)]
    public async Task MvcInbox_FailureUsesFactoryTraceIdWithoutDuplicateExtension(bool invalidQuery, int status, string code)
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var student = f.User(UserRole.Student);
        await using var db = f.Context(student);
        var services = new ServiceCollection(); services.AddLogging(); services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var controller = new NotificationsController(new(db, f.Tenant(student), f.Clock), f.Clock)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "mvc-inbox-regression" } } };
        var result = Assert.IsType<ObjectResult>(invalidQuery ? await controller.List(new() { Page = 0 }, default) : await controller.Detail("99999", default));
        Assert.Equal(status, result.StatusCode); var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(code, problem.Extensions["errorCode"]); Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    [Theory]
    [InlineData("Bài làm của học sinh")]
    [InlineData("ĐÁP ÁN: A")]
    [InlineData("ba\u200Bi lam trong anh")]
    [InlineData("Điểm của học sinh: 8.5/10")]
    [InlineData("student01: thông tin riêng")]
    [InlineData("Học sinh: tên riêng không cần gửi")]
    [InlineData("submission 123")]
    [InlineData("CCCD: 012345678901")]
    [InlineData("Liên hệ example@example.invalid")]
    [InlineData("Số liên lạc +84 912 345 678")]
    public void Relay_RejectsCommonAcademicAndPersonalSigns_InBothDirections(string body)
    {
        Assert.True(NotificationContentPolicy.IsRestricted("PlatformSupport", body));
        Assert.True(NotificationContentPolicy.IsRestricted("CenterManagersRelay", body));
    }

    [Theory]
    [InlineData("Mật khẩu: synthetic-not-a-real-credential")]
    [InlineData("API_KEY=synthetic-value")]
    [InlineData("token: synthetic-not-a-real-token")]
    [InlineData("Bearer synthetic-token-value")]
    [InlineData("eyJsyntheticvalue.syntheticpayload.syntheticsignature")]
    [InlineData("https://example.invalid/test")]
    [InlineData("C:\\private\\document.png")]
    [InlineData("<img src='x'>")]
    public void EveryChannel_RejectsCredentialLinkAndMarkupSigns(string body)
    {
        Assert.True(NotificationContentPolicy.IsRestricted("PlatformSupport", body));
        Assert.True(NotificationContentPolicy.IsRestricted("SelectedStudents", body));
    }

    [Fact]
    public void Policy_AllowsGenericSupportAndLegitimatePrivateAcademicText_AndBoundsInput()
    {
        Assert.False(NotificationContentPolicy.IsRestricted("PlatformSupport", "Hệ thống báo lỗi khi mở màn hình lớp. Vui lòng kiểm tra dịch vụ."));
        Assert.False(NotificationContentPolicy.IsRestricted("CenterManagersRelay", "Bảo trì dịch vụ lúc 21 giờ; dữ liệu được giữ nguyên."));
        Assert.False(NotificationContentPolicy.IsRestricted("SelectedStudents", "Điểm bài làm của em: 8/10, xem lời giải trong bài đã nộp."));
        Assert.True(NotificationContentPolicy.IsRestricted("PlatformSupport", new string('x', 3001)));
        Assert.True(NotificationContentPolicy.IsRestricted("PlatformSupport", "\ud800"));
    }

    [Fact]
    public async Task PreviewAndDirectSend_BlockWithoutPersistingOrGranting_AlsoCorrectionAndRetraction()
    {
        await using var f = await NotificationManualTests.Fixture.Create();
        var manager = f.User(UserRole.CenterManager); var admin = f.User(UserRole.PlatformAdmin);
        await using var db = f.Context(manager); var service = f.Service(db, manager);
        var draft = NotificationManualTests.Draft("PlatformSupport", admin.UserId);
        var safe = (await service.PreviewAsync(draft, default)).Data!;
        draft.Title = "Điểm học sinh: 9/10";
        Assert.Equal(NotificationContentPolicy.Restricted, (await service.PreviewAsync(draft, default)).ErrorCode);
        Assert.Equal(NotificationContentPolicy.Restricted, (await service.SendAsync(NotificationManualTests.SendRequest(draft, safe.Token), default)).ErrorCode);
        Assert.Empty(await db.NotificationMessages.ToArrayAsync()); Assert.Empty(await db.NotificationEvents.IgnoreQueryFilters().ToArrayAsync());
        draft.Title = "Sự cố vận hành";
        var sent = (await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default)).Data!;
        draft.CorrectionOfMessageId = sent.MessageId; draft.CorrectionReason = "Đáp án: B";
        Assert.Equal(NotificationContentPolicy.Restricted, (await service.PreviewAsync(draft, default)).ErrorCode);
        Assert.Equal(NotificationContentPolicy.Restricted, await service.RetractAsync(sent.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Điểm: 7/10" }, default));
        Assert.Single(await db.NotificationMessages.ToArrayAsync()); Assert.Empty(await db.NotificationMessageActions.ToArrayAsync());
    }

    [Theory]
    [InlineData(UserRole.CenterManager, "PlatformSupport", UserRole.PlatformAdmin)]
    [InlineData(UserRole.PlatformAdmin, "CenterManagersRelay", UserRole.CenterManager)]
    public async Task Reply_UsesStoredRelayContextAndLeavesNoReplyOrOutboxWhenBlocked(UserRole senderRole, string kind, UserRole targetRole)
    {
        await using var f = await NotificationManualTests.Fixture.Create();
        var sender = f.User(senderRole); var target = f.User(targetRole);
        await using var db = f.Context(sender); var service = f.Service(db, sender);
        var draft = NotificationManualTests.Draft(kind, kind == "CenterManagersRelay" ? target.CenterId : target.UserId); draft.AllowReply = true;
        await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default);
        var envelope = await db.NotificationEvents.IgnoreQueryFilters().SingleAsync();
        await using var rd = f.Context(target); await new NotificationDispatcher(rd, f.Clock).DispatchAsync(envelope.EventId);
        var inbox = new NotificationInboxService(rd, f.Tenant(target), f.Clock); var delivery = (await inbox.ListAsync(new(), default)).Data!.Items.Single().DeliveryId;
        Assert.Equal(NotificationContentPolicy.Restricted, (await inbox.ReplyAsync(delivery, new() { ClientReplyId = Guid.NewGuid(), Body = "Bài làm: thông tin riêng" }, default)).ErrorCode);
        Assert.Empty(await rd.NotificationReplies.ToArrayAsync()); Assert.Single(await rd.NotificationEvents.IgnoreQueryFilters().ToArrayAsync());
        Assert.True((await inbox.ReplyAsync(delivery, new() { ClientReplyId = Guid.NewGuid(), Body = "Đã tiếp nhận sự cố vận hành." }, default)).IsSuccess);
        Assert.Single(await rd.NotificationReplies.ToArrayAsync()); Assert.Equal(2, await rd.NotificationEvents.IgnoreQueryFilters().CountAsync());
    }
}
