using EduTwin.API.Notifications;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed class NotificationManualTests
{
    [Fact]
    public void Endpoints_RequireSendOrOwnSentPolicies_AndThrottleMutations()
    {
        var controller = typeof(EduTwin.API.Controllers.NotificationMessagesController);
        Assert.NotNull(Attribute.GetCustomAttribute(controller, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute)));
        foreach (var name in new[] { "Options", "Preview", "Send", "Sent", "Detail" })
        {
            var method = controller.GetMethod(name)!;
            var policy = (Microsoft.AspNetCore.Authorization.AuthorizeAttribute)Attribute.GetCustomAttribute(method, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute))!;
            Assert.Equal(name is "Sent" or "Detail" ? EduTwin.API.Security.CompositePermissionPolicies.NotificationSentRead : EduTwin.API.Security.CompositePermissionPolicies.NotificationSend, policy.Policy);
            if (name is "Preview" or "Send")
                Assert.NotNull(Attribute.GetCustomAttribute(method, typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute)));
        }
    }
    [Theory]
    [InlineData(UserRole.Teacher, "CenterManagers", UserRole.CenterManager)]
    [InlineData(UserRole.CenterManager, "CenterTeachers", UserRole.Teacher)]
    [InlineData(UserRole.CenterManager, "PlatformSupport", UserRole.PlatformAdmin)]
    [InlineData(UserRole.PlatformAdmin, "CenterManagersRelay", UserRole.CenterManager)]
    [InlineData(UserRole.Teacher, "SelectedStudents", UserRole.Student)]
    public async Task Send_MatrixRoutesOnlyValidatedRecipients_AndOwnSent(UserRole role, string kind, UserRole targetRole)
    {
        await using var fixture = await Fixture.Create();
        var sender = fixture.User(role);
        var target = fixture.User(targetRole);
        await using var db = fixture.Context(sender);
        var service = fixture.Service(db, sender);
        var draft = Draft(kind, kind == "CenterManagersRelay" ? target.CenterId : target.UserId);
        var preview = await service.PreviewAsync(draft, default);
        Assert.True(preview.IsSuccess); Assert.Equal(1, preview.Data!.RecipientCount);
        var request = SendRequest(draft, preview.Data.Token);
        var result = await service.SendAsync(request, default);
        Assert.True(result.IsSuccess);
        Assert.True((await service.SendAsync(request, default)).IsSuccess);
        Assert.Single(await db.NotificationMessages.ToListAsync());
        var ownEvent = await db.NotificationEvents.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(target.CenterId, ownEvent.CenterId);
        Assert.Equal(sender.CenterId == target.CenterId ? sender.UserId : (Guid?)null, ownEvent.CreatedBy);
        Assert.Equal("ManualNotice", ownEvent.EventType);
        Assert.Equal(sender.DisplayName, ownEvent.SenderName);
        await using var recipientDb = fixture.Context(target);
        Assert.True(await new NotificationDispatcher(recipientDb, fixture.Clock).DispatchAsync(ownEvent.EventId));
        var inbox = await new NotificationInboxService(recipientDb, fixture.Tenant(target), fixture.Clock).ListAsync(new(), default);
        Assert.Single(inbox.Data!.Items); Assert.Equal(sender.DisplayName, inbox.Data.Items[0].SenderName);
        var sent = await service.SentDetailAsync(result.Data!.MessageId, default);
        Assert.Equal(1, sent.Data!.DeliveredCount); Assert.Equal("Completed", sent.Data.DispatchStatus);
        var otherService = fixture.Service(recipientDb, target);
        Assert.Equal(target.RoleName == UserRole.Student ? ErrorCodes.ForbiddenResource : ErrorCodes.ResourceNotFound,
            (await otherService.SentDetailAsync(result.Data.MessageId, default)).ErrorCode);
    }
    [Fact]
    public async Task Preview_RejectsStudentForeignClassAndArchivedClass()
    {
        await using var f = await Fixture.Create(); var teacher = f.User(UserRole.Teacher);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        Assert.Equal(ErrorCodes.ForbiddenResource, (await service.PreviewAsync(Draft("ClassStudents", Guid.NewGuid()), default)).ErrorCode);
        (await db.Classes.SingleAsync()).Status = ClassStatus.Archived; await db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ForbiddenResource, (await service.PreviewAsync(Draft("ClassStudents", f.ClassId), default)).ErrorCode);
        var student = f.User(UserRole.Student); await using var studentDb = f.Context(student);
        Assert.Equal(ErrorCodes.ForbiddenResource, (await f.Service(studentDb, student).OptionsAsync(default)).ErrorCode);
    }
    [Fact]
    public async Task Preview_RejectsUnownedStudentAndForeignTeacher()
    {
        await using var f = await Fixture.Create(); var teacher = f.User(UserRole.Teacher);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        Assert.Equal(ErrorCodes.ForbiddenResource, (await service.PreviewAsync(Draft("SelectedStudents", f.ForeignTeacher.UserId), default)).ErrorCode);
        var manager = f.User(UserRole.CenterManager); await using var managerDb = f.Context(manager);
        Assert.Equal(ErrorCodes.ForbiddenResource, (await f.Service(managerDb, manager).PreviewAsync(Draft("CenterTeachers", f.ForeignTeacher.UserId), default)).ErrorCode);
    }
    [Fact]
    public async Task Send_RejectsEditedTamperedExpiredAndOtherSenderPreview()
    {
        await using var f = await Fixture.Create(); var teacher = f.User(UserRole.Teacher);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        var draft = Draft("CenterManagers", f.User(UserRole.CenterManager).UserId);
        var preview = (await service.PreviewAsync(draft, default)).Data!;
        var edited = SendRequest(draft, preview.Token); edited.Body = "Đã thay nội dung";
        Assert.Equal(ErrorCodes.ValidationFailed, (await service.SendAsync(edited, default)).ErrorCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await service.SendAsync(SendRequest(draft, "invalid token"), default)).ErrorCode);
        var manager = f.User(UserRole.CenterManager);
        await using var otherDb = f.Context(manager);
        Assert.Equal(ErrorCodes.ValidationFailed, (await f.Service(otherDb, manager).SendAsync(SendRequest(draft, preview.Token), default)).ErrorCode);
        await using var foreignDb = f.Context(f.ForeignTeacher);
        Assert.Equal(ErrorCodes.ValidationFailed, (await f.Service(foreignDb, f.ForeignTeacher).SendAsync(SendRequest(draft, preview.Token), default)).ErrorCode);
        f.Clock.Now = f.Clock.Now.AddMinutes(6);
        Assert.Equal(ErrorCodes.ValidationFailed, (await service.SendAsync(SendRequest(draft, preview.Token), default)).ErrorCode);
        Assert.Empty(await db.NotificationMessages.ToListAsync());
    }
    [Fact]
    public async Task Send_ChangedAudienceNeedsNewPreview_AndClientIdCollisionDoesNotSendAgain()
    {
        await using var f = await Fixture.Create(); var teacher = f.User(UserRole.Teacher);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher);
        var draft = Draft("ClassStudents", f.ClassId); var token = (await service.PreviewAsync(draft, default)).Data!.Token;
        (await db.ClassStudents.SingleAsync()).Status = ClassStudentStatus.Removed; await db.SaveChangesAsync();
        Assert.Equal(NotificationManualService.ScopeChanged, (await service.SendAsync(SendRequest(draft, token), default)).ErrorCode);
        Assert.Empty(await db.NotificationMessages.ToListAsync());
        (await db.ClassStudents.SingleAsync()).Status = ClassStudentStatus.Active; await db.SaveChangesAsync();
        var request = SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token);
        Assert.True((await service.SendAsync(request, default)).IsSuccess);
        request.Title = "Khác";
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await service.SendAsync(request, default)).ErrorCode);
        Assert.Single(await db.NotificationMessages.ToListAsync());
    }
    [Theory]
    [InlineData("member")]
    [InlineData("archived")]
    [InlineData("sender_version")]
    [InlineData("sender_locked")]
    public async Task Dispatch_RevalidatesAfterSendAndSuppressesObsoleteScope(string change)
    {
        await using var f = await Fixture.Create(); var teacher = f.User(UserRole.Teacher);
        await using var db = f.Context(teacher); var service = f.Service(db, teacher); var draft = Draft("ClassStudents", f.ClassId);
        var result = await service.SendAsync(SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default);
        Assert.True(result.IsSuccess);
        if (change == "member") (await db.ClassStudents.SingleAsync()).Status = ClassStudentStatus.Removed;
        if (change == "archived") (await db.Classes.SingleAsync()).Status = ClassStatus.Archived;
        if (change == "sender_version") (await db.Users.SingleAsync(u => u.UserId == teacher.UserId)).AuthVersion++;
        if (change == "sender_locked") (await db.Users.SingleAsync(u => u.UserId == teacher.UserId)).Status = UserStatus.Locked;
        await db.SaveChangesAsync();
        var ev = await db.NotificationEvents.SingleAsync();
        Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(ev.EventId));
        Assert.Empty(await db.NotificationDeliveries.ToListAsync());
        Assert.Equal(1, (await db.NotificationDispatches.SingleAsync()).SuppressedCount);
    }
    [Fact]
    public void Permissions_DoNotGiveStudentsSendOrManagersAcademicAccess()
    {
        Assert.DoesNotContain("notifications.messages.send_scoped", AuthorizationPermissionCatalog.SystemStudentDefaultCodes);
        Assert.Contains("notifications.messages.send_scoped", AuthorizationPermissionCatalog.SystemTeacherDefaultCodes);
        Assert.Contains("notifications.messages.send_scoped", AuthorizationPermissionCatalog.SystemCenterManagerDefaultCodes);
        Assert.False(AuthorizationPermissionCatalog.CreatePermissions().Single(x => x.PermissionCode == "platform.notifications.send_scoped").IsDelegable);
        Assert.DoesNotContain(AuthorizationPermissionCatalog.SystemCenterManagerDefaultCodes, p => AuthorizationPermissionCatalog.AcademicOperationalCodes.Contains(p));
    }
    internal static NotificationComposeRequest Draft(string kind, params Guid[] ids) => new() { Title = "Thông báo cô lập", Body = "Nội dung không phát cho dữ liệu thật", AudienceKind = kind, TargetIds = ids };
    internal static NotificationSendRequest SendRequest(NotificationComposeRequest r, string token) => new() { Title = r.Title, Body = r.Body, AudienceKind = r.AudienceKind, TargetIds = r.TargetIds, PreviewToken = token, ClientMessageId = Guid.NewGuid(),
        Priority = r.Priority, Category = r.Category, RequiresAcknowledgement = r.RequiresAcknowledgement, AllowReply = r.AllowReply, CorrectionOfMessageId = r.CorrectionOfMessageId, CorrectionReason = r.CorrectionReason };
    internal sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly string name = Guid.NewGuid().ToString();
        private readonly List<User> users = [];
        public Clock Clock { get; } = new();
        public Guid ClassId { get; } = Guid.NewGuid();
        public User ForeignTeacher { get; private set; } = null!;
        private readonly INotificationPreviewProtector protector = new NotificationPreviewProtector(new EphemeralDataProtectionProvider());
        public User User(UserRole role) => users.First(u => u.RoleName == role);
        public TenantContext Tenant(User user) { var t = new TenantContext(); t.Initialize(user.CenterId, user.UserId, user.RoleName.ToString(), 1); return t; }
        public EduTwinDbContext Context(User user, bool observeBusiness = false)
        {
            var tenant = Tenant(user); var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(name);
            if (observeBusiness) options.AddInterceptors(new NotificationBusinessInterceptor(tenant, Clock));
            return new(options.Options, tenant);
        }
        public NotificationManualService Service(EduTwinDbContext db, User user) => new(db, Tenant(user), Clock, new(db), protector);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); var center = Guid.NewGuid(); var platform = Guid.NewGuid(); var foreign = Guid.NewGuid(); var now = DateTime.UtcNow;
            foreach (var role in new[] { UserRole.Teacher, UserRole.CenterManager, UserRole.PlatformAdmin, UserRole.Student })
                f.users.Add(new User { UserId = Guid.NewGuid(), CenterId = role == UserRole.PlatformAdmin ? platform : center, RoleName = role, Status = UserStatus.Active,
                    Username = Guid.NewGuid().ToString("N"), DisplayName = role.ToString(), PasswordHash = "not-a-password", AuthVersion = 1, CreatedAt = now, UpdatedAt = now });
            f.ForeignTeacher = new User { UserId = Guid.NewGuid(), CenterId = foreign, RoleName = UserRole.Teacher, Status = UserStatus.Active,
                Username = "foreign", DisplayName = "Outside scope", PasswordHash = "not-a-password", AuthVersion = 1, CreatedAt = now, UpdatedAt = now };
            await using var db = f.Context(f.User(UserRole.Teacher));
            foreach (var (id, code) in new[] { (center, "TEST_A"), (platform, "PLATFORM"), (foreign, "TEST_B") })
                db.Centers.Add(new Center { CenterId = id, CenterCode = code, CenterName = code, Status = CenterStatus.Active, Timezone = "Asia/Ho_Chi_Minh", CreatedAt = now, UpdatedAt = now });
            db.Users.AddRange(f.users); db.Users.Add(f.ForeignTeacher);
            db.Classes.Add(new Class { ClassId = f.ClassId, CenterId = center, TeacherId = f.User(UserRole.Teacher).UserId, SubjectId = Guid.NewGuid(), ClassName = "Lớp kiểm thử", AcademicYear = "2026", Status = ClassStatus.Active, LearningScope = ClassLearningScope.Current, CreatedAt = now, UpdatedAt = now });
            db.ClassStudents.Add(new ClassStudent { CenterId = center, ClassId = f.ClassId, StudentId = f.User(UserRole.Student).UserId, Status = ClassStudentStatus.Active, JoinedAt = now });
            await db.SaveChangesAsync(); return f;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
