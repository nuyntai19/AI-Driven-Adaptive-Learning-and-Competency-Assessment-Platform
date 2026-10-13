using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.API.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

[Collection("MySqlDatabase")]
public sealed partial class NotificationMySqlTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const string ConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";

    [NotificationMySqlFact]
    public async Task Lifecycle_ReceiptsRepliesAndRetraction_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.CenterManager); await using var db = f.Context();
        var target = await db.Users.Where(u => u.UserId != f.UserId).Select(u => u.UserId).SingleAsync();
        var service = new NotificationManualService(db, f.Tenant, TimeProvider.System, new(db), new NotificationPreviewProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()));
        var draft = NotificationManualTests.Draft("CenterTeachers", target); draft.Priority = "Important"; draft.RequiresAcknowledgement = true; draft.AllowReply = true;
        var sent = (await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default)).Data!;
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync((await db.NotificationEvents.SingleAsync()).EventId));
        var recipient = new TenantContext(); recipient.Initialize(f.CenterId, target, "Teacher", 1);
        await using var rd = f.Context(recipient); var inbox = new NotificationInboxService(rd, recipient, TimeProvider.System);
        var item = (await inbox.ListAsync(new() { Priority = "Important", NeedsAcknowledgement = true }, default)).Data!.Items.Single();
        Assert.Null(await inbox.AcknowledgeAsync(item.DeliveryId, default)); Assert.Null(await inbox.AcknowledgeAsync(item.DeliveryId, default));
        Assert.Null((await inbox.DetailAsync(item.DeliveryId, default)).Data!.ReadAt);
        Assert.Empty((await inbox.ListAsync(new() { NeedsAcknowledgement = true }, default)).Data!.Items);
        Assert.True((await inbox.ReplyAsync(item.DeliveryId, new() { ClientReplyId = Guid.NewGuid(), Body = "Phản hồi riêng cô lập" }, default)).IsSuccess);
        Assert.Single((await service.RepliesAsync(sent.MessageId, new(), default)).Data!.Items);
        Assert.Null(await inbox.HideAsync(item.DeliveryId, default)); Assert.Empty((await inbox.ListAsync(new(), default)).Data!.Items);
        await Assert.ThrowsAsync<MySqlException>(() => rd.Database.ExecuteSqlRawAsync("UPDATE notification_deliveries SET acknowledged_at=NULL"));
        await Assert.ThrowsAsync<MySqlException>(() => rd.Database.ExecuteSqlRawAsync("UPDATE notification_deliveries SET hidden_at=NULL"));
        await Assert.ThrowsAsync<MySqlException>(() => rd.Database.ExecuteSqlRawAsync("DELETE FROM notification_replies"));
        await Assert.ThrowsAsync<MySqlException>(() => rd.Database.ExecuteSqlRawAsync("UPDATE notification_replies SET body='overwrite'"));
        Assert.Null(await service.RetractAsync(sent.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Lịch mới" }, default));
        Assert.Equal("Retracted", (await inbox.DetailAsync(item.DeliveryId, default)).Data!.LifecycleStatus);
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE notification_message_actions SET reason='overwrite'"));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM notification_message_actions"));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [NotificationMySqlFact]
    public async Task Lifecycle_CorrectionIsAtomic_AndSingleActionWins_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.CenterManager); await using var db = f.Context();
        var target = await db.Users.Where(u => u.UserId != f.UserId).Select(u => u.UserId).SingleAsync();
        var protector = new NotificationPreviewProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        var service = new NotificationManualService(db, f.Tenant, TimeProvider.System, new(db), protector);
        var draft = NotificationManualTests.Draft("CenterTeachers", target);
        var original = (await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default)).Data!;
        draft.CorrectionOfMessageId = original.MessageId; draft.CorrectionReason = "Sửa thông tin"; draft.Body = "Bản thay thế";
        var request = NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER deny_test_action_insert BEFORE INSERT ON notification_message_actions FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Isolated failure'");
        await Assert.ThrowsAsync<DbUpdateException>(() => service.SendAsync(request, default));
        Assert.Single(await db.NotificationMessages.ToListAsync()); Assert.Single(await db.NotificationEvents.ToListAsync()); Assert.Empty(await db.NotificationMessageActions.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER deny_test_action_insert");
        var result = await service.SendAsync(request, default); Assert.True(result.IsSuccess);
        Assert.Equal(result.Data!.MessageId, (await service.SendAsync(request, default)).Data!.MessageId);
        Assert.Equal("Corrected", (await service.SentDetailAsync(original.MessageId, default)).Data!.LifecycleStatus);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await service.RetractAsync(original.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Không thể thu hồi bản đã thay" }, default));
        Assert.Equal(2, await db.NotificationMessages.CountAsync()); Assert.Single(await db.NotificationMessageActions.ToListAsync());
    }

    [NotificationMySqlFact]
    public async Task TeacherClassStudents_QueryAndArchivedRevalidation_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student); await using var db = f.Context();
        var studentId = await db.Users.Where(u => u.UserId != f.UserId).Select(u => u.UserId).SingleAsync();
        var now = DateTime.UtcNow; var subjectId = Guid.NewGuid(); var classId = Guid.NewGuid();
        db.Teachers.Add(new EduTwin.DAL.Organization.Teacher { CenterId = f.CenterId, TeacherId = f.UserId, CreatedAt = now, UpdatedAt = now });
        db.Students.Add(new EduTwin.DAL.Organization.Student { CenterId = f.CenterId, StudentId = studentId, FullName = "Isolated student", GradeLevel = 10, CreatedAt = now, UpdatedAt = now });
        db.Subjects.Add(new EduTwin.DAL.Organization.Subject { CenterId = f.CenterId, SubjectId = subjectId, SubjectCode = "TEST_MATH", SubjectName = "Isolated math", IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Classes.Add(new EduTwin.DAL.Organization.Class { CenterId = f.CenterId, ClassId = classId, TeacherId = f.UserId, SubjectId = subjectId, ClassName = "Isolated class", AcademicYear = "2026-2027", GradeLevel = 10, Status = EduTwin.Contracts.Organization.ClassStatus.Active, LearningScope = EduTwin.Contracts.Organization.ClassLearningScope.Current, CreatedAt = now, UpdatedAt = now });
        db.ClassStudents.Add(new EduTwin.DAL.Organization.ClassStudent { CenterId = f.CenterId, ClassId = classId, StudentId = studentId, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, JoinedAt = now, GradeLevelAtEnrollment = 10 });
        await db.SaveChangesAsync();
        var service = new NotificationManualService(db, f.Tenant, TimeProvider.System, new(db), new NotificationPreviewProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()));
        Assert.Single((await service.OptionsAsync(default)).Data!.Single(o => o.Kind == "SelectedStudents").Choices);
        var draft = NotificationManualTests.Draft("ClassStudents", classId);
        var preview = await service.PreviewAsync(draft, default); Assert.True(preview.IsSuccess); Assert.Equal(1, preview.Data!.RecipientCount);
        Assert.True((await service.SendAsync(NotificationManualTests.SendRequest(draft, preview.Data.Token), default)).IsSuccess);
        var archivedClass = await db.Classes.SingleAsync();
        archivedClass.Status = EduTwin.Contracts.Organization.ClassStatus.Archived;
        archivedClass.LearningScope = EduTwin.Contracts.Organization.ClassLearningScope.History;
        await db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.ForbiddenResource, (await service.PreviewAsync(draft, default)).ErrorCode);
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync((await db.NotificationEvents.SingleAsync()).EventId));
        Assert.Empty(await db.NotificationDeliveries.ToListAsync());
        Assert.Equal(1, (await db.NotificationDispatches.SingleAsync()).SuppressedCount);
    }

    [NotificationMySqlFact]
    public async Task ManualSend_RollbackImmutableHistoryAndIdempotency_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.CenterManager); await using var db = f.Context();
        var target = await db.Users.Where(u => u.UserId != f.UserId).Select(u => u.UserId).SingleAsync();
        var protector = new NotificationPreviewProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        var service = new NotificationManualService(db, f.Tenant, TimeProvider.System, new(db), protector);
        var draft = NotificationManualTests.Draft("CenterTeachers", target);
        var token = (await service.PreviewAsync(draft, default)).Data!.Token;
        var request = NotificationManualTests.SendRequest(draft, token);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER deny_test_message_insert BEFORE INSERT ON notification_messages FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Injected isolated test failure'");
        await Assert.ThrowsAsync<DbUpdateException>(() => service.SendAsync(request, default));
        Assert.Empty(await db.NotificationMessages.ToListAsync()); Assert.Empty(await db.NotificationEvents.ToListAsync()); Assert.Empty(await db.NotificationDispatches.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER deny_test_message_insert");
        var first = await service.SendAsync(request, default); Assert.True(first.IsSuccess);
        var second = await service.SendAsync(request, default); Assert.Equal(first.Data!.MessageId, second.Data!.MessageId);
        Assert.Single(await db.NotificationMessages.ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE notification_messages SET title='overwrite'"));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM notification_messages"));
    }

    [NotificationMySqlFact]
    public async Task ManualSend_ConcurrentClientIdOnlyOneMessage_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.CenterManager); await using var setup = f.Context();
        var target = await setup.Users.Where(u => u.UserId != f.UserId).Select(u => u.UserId).SingleAsync();
        var protector = new NotificationPreviewProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        var draft = NotificationManualTests.Draft("CenterTeachers", target);
        var preview = await new NotificationManualService(setup, f.Tenant, TimeProvider.System, new(setup), protector).PreviewAsync(draft, default);
        var request = NotificationManualTests.SendRequest(draft, preview.Data!.Token);
        await using var a = f.Context(); await using var b = f.Context();
        var results = await Task.WhenAll(new NotificationManualService(a, f.Tenant, TimeProvider.System, new(a), protector).SendAsync(request, default),
            new NotificationManualService(b, f.Tenant, TimeProvider.System, new(b), protector).SendAsync(request, default));
        Assert.All(results, r => Assert.True(r.IsSuccess)); Assert.Equal(results[0].Data!.MessageId, results[1].Data!.MessageId);
        Assert.Single(await setup.NotificationMessages.AsNoTracking().ToListAsync()); Assert.Single(await setup.NotificationEvents.ToListAsync());
    }

    [NotificationMySqlFact]
    public async Task ManualRelay_CompositeSenderFkAndLocalEnvelope_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.CenterManager); await using var db = f.Context();
        var platform = Guid.NewGuid(); var admin = Guid.NewGuid(); var now = DateTime.UtcNow;
        db.Centers.Add(new EduTwin.DAL.Organization.Center { CenterId = platform, CenterCode = "PLATFORM", CenterName = "Isolated platform", Timezone = "Asia/Ho_Chi_Minh", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = now, UpdatedAt = now });
        db.Users.Add(new EduTwin.DAL.IdentityAndTenancy.User { UserId = admin, CenterId = platform, Username = "admin_test", DisplayName = "Isolated admin", PasswordHash = "not-a-password", RoleName = EduTwin.Contracts.IdentityAndTenancy.UserRole.PlatformAdmin, Status = EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active, AuthVersion = 1, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var service = new NotificationManualService(db, f.Tenant, TimeProvider.System, new(db), new NotificationPreviewProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()));
        var draft = NotificationManualTests.Draft("PlatformSupport", admin);
        draft.Body = "Điểm học sinh: 8/10";
        Assert.Equal(NotificationContentPolicy.Restricted, (await service.PreviewAsync(draft, default)).ErrorCode);
        Assert.Equal(NotificationContentPolicy.Restricted, (await service.SendAsync(NotificationManualTests.SendRequest(draft, "direct-call-no-bypass"), default)).ErrorCode);
        Assert.Empty(await db.NotificationMessages.ToArrayAsync()); Assert.Empty(await db.NotificationEvents.IgnoreQueryFilters().ToArrayAsync());
        draft.Body = "Sự cố vận hành cô lập cần kiểm tra."; draft.AllowReply = true;
        var result = await service.SendAsync(NotificationManualTests.SendRequest(draft, (await service.PreviewAsync(draft, default)).Data!.Token), default);
        Assert.True(result.IsSuccess);
        var envelope = await db.NotificationEvents.IgnoreQueryFilters().SingleAsync(); Assert.Equal(platform, envelope.CenterId); Assert.Null(envelope.CreatedBy);
        var targetScope = new TenantContext(); targetScope.Initialize(platform, admin, "PlatformAdmin", 1);
        await using var recipient = f.Context(targetScope);
        Assert.True(await new NotificationDispatcher(recipient, TimeProvider.System).DispatchAsync(envelope.EventId));
        Assert.Single((await new NotificationInboxService(recipient, targetScope, TimeProvider.System).ListAsync(new(), default)).Data!.Items);
        Assert.Equal(1, (await service.SentDetailAsync(result.Data!.MessageId, default)).Data!.DeliveredCount);
        var targetSent = await new NotificationManualService(recipient, targetScope, TimeProvider.System, new(recipient),
            new NotificationPreviewProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider())).SentAsync(new(), default);
        Assert.True(targetSent.IsSuccess);
        Assert.Empty(targetSent.Data!.Items);
        var inbox = new NotificationInboxService(recipient, targetScope, TimeProvider.System);
        var delivery = (await inbox.ListAsync(new(), default)).Data!.Items.Single().DeliveryId;
        Assert.Equal(NotificationContentPolicy.Restricted, (await inbox.ReplyAsync(delivery, new() { ClientReplyId = Guid.NewGuid(), Body = "Đáp án: thông tin riêng" }, default)).ErrorCode);
        Assert.Empty(await recipient.NotificationReplies.ToArrayAsync()); Assert.Single(await recipient.NotificationEvents.IgnoreQueryFilters().ToArrayAsync());
        Assert.True((await inbox.ReplyAsync(delivery, new() { ClientReplyId = Guid.NewGuid(), Body = "Đã tiếp nhận sự cố vận hành." }, default)).IsSuccess);
        Assert.Single(await recipient.NotificationReplies.ToArrayAsync()); Assert.Equal(2, await recipient.NotificationEvents.IgnoreQueryFilters().CountAsync());
        Assert.Equal(NotificationContentPolicy.Restricted, await service.RetractAsync(result.Data.MessageId, new() { ClientActionId = Guid.NewGuid(), Reason = "Bài làm riêng" }, default));
        draft.CorrectionOfMessageId = result.Data.MessageId; draft.CorrectionReason = "CCCD: 012345678901";
        Assert.Equal(NotificationContentPolicy.Restricted, (await service.PreviewAsync(draft, default)).ErrorCode);
        Assert.Empty(await db.NotificationMessageActions.ToArrayAsync());
    }

    [NotificationMySqlFact]
    public async Task BackgroundDiscovery_UsesScopedWorker_OneEventPerTenantPerBatch()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Stage("batch-first"); await fixture.Stage("batch-second");
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TenantContext>();
        services.AddScoped<IBackgroundTenantScopeFactory>(s => s.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantIdAccessor>(s => s.GetRequiredService<TenantContext>());
        services.AddDbContext<EduTwinDbContext>(b => b.UseMySQL(fixture.Connection));
        services.AddNotifications();
        await using var provider = services.BuildServiceProvider();
        var worker = new NotificationBackgroundService(provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            Options.Create(new NotificationWorkerOptions()), NullLogger<NotificationBackgroundService>.Instance);
        await worker.RunBatchAsync(default);
        await using var db = fixture.Context();
        Assert.Single(await db.NotificationDeliveries.AsNoTracking().ToListAsync());
        await worker.RunBatchAsync(default);
        Assert.Equal(2, await db.NotificationDeliveries.CountAsync());
    }

    [NotificationMySqlFact]
    public async Task AtomicOutbox_RollbackLeavesNoEvent_AndMigrationHasNoDrift()
    {
        await using var fixture = await Fixture.Create();
        await using var db = fixture.Context();
        Assert.False(db.Database.HasPendingModelChanges());
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            (await db.Users.SingleAsync(u => u.UserId == fixture.UserId)).DisplayName = "Rolled back business change";
            await new NotificationOutbox(db, fixture.Tenant, TimeProvider.System).StageNoticeAsync("rollback", "Kiểm thử", "Bản cô lập", [fixture.UserId]);
            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Empty(await db.NotificationEvents.ToListAsync());
        Assert.Empty(await db.NotificationDispatches.ToListAsync());
        Assert.Equal("Isolated test user", (await db.Users.SingleAsync(u => u.UserId == fixture.UserId)).DisplayName);
    }

    [NotificationMySqlFact]
    public async Task UniqueDeliveryAndCompositeForeignKey_BlockDuplicatesAndForeignUser()
    {
        await using var fixture = await Fixture.Create();
        var eventId = await fixture.Stage("unique");
        await using var db = fixture.Context();
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(eventId));
        db.ChangeTracker.Clear();
        db.NotificationDeliveries.Add(new() { CenterId = fixture.CenterId, EventId = eventId, UserId = fixture.UserId, DeliveredAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.NotificationDeliveries.Add(new() { CenterId = fixture.CenterId, EventId = eventId, UserId = Guid.NewGuid(), DeliveredAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Single(await db.NotificationDeliveries.ToListAsync());
    }

    [NotificationMySqlFact]
    public async Task ConcurrentWorkers_OnlyOneDelivery_RestartRecoversExpiredLease()
    {
        await using var fixture = await Fixture.Create();
        var eventId = await fixture.Stage("concurrent");
        await using var a = fixture.Context(); await using var b = fixture.Context();
        await Task.WhenAll(new NotificationDispatcher(a, TimeProvider.System).DispatchAsync(eventId),
            new NotificationDispatcher(b, TimeProvider.System).DispatchAsync(eventId));
        await using var check = fixture.Context();
        Assert.Single(await check.NotificationDeliveries.ToListAsync());
        Assert.Equal("Completed", (await check.NotificationDispatches.SingleAsync()).State);
        var nextId = await fixture.Stage("restart");
        var state = await check.NotificationDispatches.SingleAsync(x => x.EventId == nextId);
        state.State = "Processing"; state.LeaseToken = Guid.NewGuid(); state.LeaseUntil = DateTime.UtcNow.AddSeconds(-1); state.AttemptCount = 1;
        await check.SaveChangesAsync();
        await using var restarted = fixture.Context();
        Assert.True(await new NotificationDispatcher(restarted, TimeProvider.System).DispatchAsync(nextId));
        Assert.Equal(2, await restarted.NotificationDeliveries.CountAsync());
    }

    [NotificationMySqlFact]
    public async Task ImmutableHistoryAndReadSnapshot_AreEnforcedInRealSql()
    {
        await using var fixture = await Fixture.Create();
        var firstId = await fixture.Stage("first");
        await using var db = fixture.Context();
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(firstId));
        var inbox = new NotificationInboxService(db, fixture.Tenant, TimeProvider.System);
        var ids = (await inbox.ListAsync(new(), default)).Data!.Items.Select(x => x.DeliveryId).ToArray();
        var secondId = await fixture.Stage("second");
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(secondId));
        Assert.Null(await inbox.ReadAsync(ids, default));
        Assert.Null(await inbox.ReadAsync(ids, default));
        Assert.Equal(1, (await inbox.SummaryAsync(default)).Data!.UnreadCount);
        Assert.Equal(ErrorCodes.ResourceNotFound, await inbox.ReadAsync([ids[0], ulong.MaxValue.ToString()], default));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE notification_events SET title='overwrite'"));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM notification_events"));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE notification_deliveries SET read_at=NULL WHERE read_at IS NOT NULL"));
        await Assert.ThrowsAsync<MySqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM notification_deliveries"));
    }

    [NotificationMySqlFact]
    public async Task CrossTenantInboxAndDeliveryAreFailClosed()
    {
        await using var fixture = await Fixture.Create();
        var id = await fixture.Stage("scope");
        await using var db = fixture.Context();
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(id));
        var delivery = await db.NotificationDeliveries.SingleAsync();
        var foreign = new TenantContext(); foreign.Initialize(Guid.NewGuid(), fixture.UserId, "Teacher", 1);
        await using var other = fixture.Context(foreign);
        Assert.Empty(await other.NotificationEvents.ToListAsync());
        Assert.False(await new NotificationDispatcher(other, TimeProvider.System).DispatchAsync(id));
        var inbox = new NotificationInboxService(other, foreign, TimeProvider.System);
        Assert.Equal(0, (await inbox.SummaryAsync(default)).Data!.UnreadCount);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await inbox.DetailAsync(delivery.DeliveryId.ToString(), default)).ErrorCode);
    }

    private sealed class NotificationMySqlFactAttribute : FactAttribute
    {
        public NotificationMySqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable))) Skip = "Isolated MySQL admin connection required.";
        }
    }
    private sealed class Fixture(string admin, string database, string connection, TenantContext tenant) : IAsyncDisposable
    {
        public TenantContext Tenant => tenant;
        public string Connection => connection;
        public Guid CenterId => tenant.CenterId!.Value;
        public Guid UserId => tenant.UserId!.Value;
        public EduTwinDbContext Context(TenantContext? scope = null, bool observeBusiness = false, Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor? counter = null)
        {
            var t = scope ?? tenant; var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseMySQL(connection);
            if (observeBusiness) options.AddInterceptors(new NotificationBusinessInterceptor(t, TimeProvider.System));
            if (counter != null) options.AddInterceptors(counter);
            return new(options.Options, t);
        }
        public async Task<Guid> Stage(string key)
        {
            await using var db = Context();
            var id = await new NotificationOutbox(db, tenant, TimeProvider.System).StageNoticeAsync(key, "Thông báo cô lập", "Không phát cho dữ liệu người dùng thật", [UserId]);
            await db.SaveChangesAsync(); return id;
        }
        public static async Task<Fixture> Create(UserRole senderRole = UserRole.Teacher, UserRole recipientRole = UserRole.Teacher, string? initialMigration = null)
        {
            var builder = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable)!) { Database = "", Pooling = false };
            var admin = builder.ConnectionString; var database = "edutwin_notify_" + Guid.NewGuid().ToString("N");
            await using (var db = new MySqlConnection(admin))
            {
                await db.OpenAsync(); await using var command = db.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{database}` CHARACTER SET utf8mb4"; await command.ExecuteNonQueryAsync();
            }
            builder.Database = database;
            var tenant = new TenantContext(); tenant.Initialize(Guid.NewGuid(), Guid.NewGuid(), senderRole.ToString(), 1);
            var fixture = new Fixture(admin, database, builder.ConnectionString, tenant);
            try
            {
                await using var db = fixture.Context();
                if (initialMigration == null) await db.Database.MigrateAsync();
                else await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(initialMigration);
                await NotificationFoundationTests.Seed(db, tenant, senderRole, recipientRole); return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public ValueTask DisposeAsync()
        {
            // Large fixture DDL can exceed the driver's default 30-second timeout.
            // The async timeout/drain path can wait forever after the schema has
            // already disappeared. Use a bounded synchronous admin operation;
            // errors still fail the test instead of hiding failed cleanup.
            using var db = new MySqlConnection(admin); db.Open();
            using var command = db.CreateCommand();
            command.CommandTimeout = 120;
            command.CommandText = $"DROP DATABASE IF EXISTS `{database}`";
            command.ExecuteNonQuery();
            return ValueTask.CompletedTask;
        }
    }
}
