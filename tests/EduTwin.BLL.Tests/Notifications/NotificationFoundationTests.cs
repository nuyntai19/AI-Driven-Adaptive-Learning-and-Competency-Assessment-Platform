using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed class NotificationFoundationTests
{
    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.CenterManager)]
    [InlineData(UserRole.PlatformAdmin)]
    public void DefaultPermissions_OnlyOwnInboxForAccountType(UserRole role)
    {
        var codes = AuthorizationPermissionCatalog.GetDefaultSystemRoleCodes(role);
        var prefix = role == UserRole.PlatformAdmin ? "platform.notifications" : "notifications.inbox";
        Assert.Contains(prefix + ".read_own", codes);
        Assert.Contains(prefix + ".update_own", codes);
        Assert.DoesNotContain(role == UserRole.PlatformAdmin ? "notifications.inbox.read_own" : "platform.notifications.read_own", codes);
        if (role == UserRole.PlatformAdmin)
            Assert.False(AuthorizationPermissionCatalog.CreatePermissions().Single(p => p.PermissionCode == prefix + ".read_own").IsDelegable);
    }

    [Fact]
    public async Task Stage_DoesNotCommit_DedupesAndRejectsKeyCollision()
    {
        var (tenant, db) = Create(); await using var owned = db;
        var recipient = await Seed(db, tenant);
        var outbox = new NotificationOutbox(db, tenant, TimeProvider.System);
        var first = await outbox.StageNoticeAsync("test:one", "Thông báo kiểm thử", "Nội dung riêng", [recipient]);
        Assert.Empty(await db.NotificationEvents.AsNoTracking().ToArrayAsync());
        Assert.Equal(first, await outbox.StageNoticeAsync("test:one", "Thông báo kiểm thử", "Nội dung riêng", [recipient]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => outbox.StageNoticeAsync("test:one", "Khác", "Nội dung riêng", [recipient]));
        await db.SaveChangesAsync();
        Assert.Single(await db.NotificationEvents.ToListAsync());
        Assert.Single(await db.NotificationDispatches.ToListAsync());
        Assert.Empty(await db.NotificationDeliveries.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stage_DedupesPersistedRecipientsRegardlessOfJsonFormattingAndOrder(bool reverse)
    {
        var (tenant, db) = Create(); await using var owned = db;
        var recipient = await Seed(db, tenant);
        var recipients = new[] { recipient, tenant.UserId!.Value }.Order().ToArray();
        var outbox = new NotificationOutbox(db, tenant, TimeProvider.System);
        var id = await outbox.StageNoticeAsync("test:formatted", "Thông báo", "Nội dung", recipients);
        var storedIds = reverse ? recipients.Reverse().ToArray() : recipients;
        db.NotificationEvents.Local.Single().RecipientUserIds =
            $"[\"{storedIds[0]}\", \"{storedIds[1]}\"]";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(id, await outbox.StageNoticeAsync("test:formatted", "Thông báo", "Nội dung", recipients));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            outbox.StageNoticeAsync("test:formatted", "Thông báo", "Nội dung", [recipient]));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            outbox.StageNoticeAsync("test:formatted", "Thông báo", "Nội dung khác", recipients));
        Assert.Single(await db.NotificationEvents.ToListAsync());
        Assert.Single(await db.NotificationDispatches.ToListAsync());
    }

    [Fact]
    public async Task Stage_RejectsForeignOrLockedRecipientAndUnresolvedTenant()
    {
        var (tenant, db) = Create(); await using var owned = db;
        var recipient = await Seed(db, tenant);
        var outbox = new NotificationOutbox(db, tenant, TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => outbox.StageNoticeAsync("a", "a", "a", [Guid.NewGuid()]));
        (await db.Users.SingleAsync(x => x.UserId == recipient)).Status = UserStatus.Locked;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => outbox.StageNoticeAsync("a", "a", "a", [recipient]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new NotificationOutbox(db, new TenantContext(), TimeProvider.System)
            .StageNoticeAsync("a", "a", "a", [recipient]));
    }

    [Fact]
    public async Task Dispatch_IdempotentAndSuppressesLockedUser()
    {
        var (tenant, db) = Create(); await using var owned = db;
        var recipient = await Seed(db, tenant);
        var outbox = new NotificationOutbox(db, tenant, TimeProvider.System);
        var id = await outbox.StageNoticeAsync("one", "Thông báo", "Nội dung", [recipient, tenant.UserId!.Value]);
        await db.SaveChangesAsync();
        (await db.Users.SingleAsync(x => x.UserId == recipient)).Status = UserStatus.Locked;
        await db.SaveChangesAsync();
        var dispatcher = new NotificationDispatcher(db, TimeProvider.System);
        Assert.True(await dispatcher.DispatchAsync(id));
        Assert.False(await dispatcher.DispatchAsync(id));
        Assert.Single(await db.NotificationDeliveries.ToListAsync());
        var state = await db.NotificationDispatches.SingleAsync();
        Assert.Equal(1, state.DeliveredCount); Assert.Equal(1, state.SuppressedCount); Assert.Equal("Completed", state.State);
    }

    [Fact]
    public async Task Dispatch_RecoversExpiredLeaseButDoesNotStealActiveLease()
    {
        var (tenant, db) = Create(); await using var owned = db; await Seed(db, tenant);
        var id = await new NotificationOutbox(db, tenant, TimeProvider.System).StageNoticeAsync("lease", "Thông báo", "Nội dung", [tenant.UserId!.Value]);
        await db.SaveChangesAsync();
        var state = await db.NotificationDispatches.SingleAsync();
        state.State = "Processing"; state.LeaseToken = Guid.NewGuid(); state.LeaseUntil = DateTime.UtcNow.AddMinutes(1);
        await db.SaveChangesAsync();
        Assert.False(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(id));
        state.LeaseUntil = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(id));
        Assert.Single(await db.NotificationDeliveries.ToListAsync());
    }

    [Fact]
    public async Task Dispatch_DeadLettersBoundedRetries()
    {
        var (tenant, db) = Create(); await using var owned = db; await Seed(db, tenant);
        var id = await new NotificationOutbox(db, tenant, TimeProvider.System).StageNoticeAsync("limit", "Thông báo", "Nội dung", [tenant.UserId!.Value]);
        await db.SaveChangesAsync(); var state = await db.NotificationDispatches.SingleAsync();
        state.AttemptCount = 5; await db.SaveChangesAsync();
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(id));
        Assert.Equal("DeadLetter", state.State); Assert.Empty(await db.NotificationDeliveries.ToListAsync());
    }

    [Fact]
    public async Task Inbox_ReadSnapshotDoesNotReadNewArrivals_AndPreservesFirstReadTime()
    {
        var (tenant, db) = Create(); await using var owned = db; await Seed(db, tenant);
        await Deliver(db, tenant, "first");
        var inbox = new NotificationInboxService(db, tenant, TimeProvider.System);
        var snapshot = (await inbox.ListAsync(new(), default)).Data!.Items.Select(x => x.DeliveryId).ToArray();
        await Deliver(db, tenant, "second");
        Assert.Null(await inbox.ReadAsync(snapshot, default));
        var before = (await inbox.DetailAsync(snapshot[0], default)).Data!.ReadAt;
        Assert.Null(await inbox.ReadAsync(snapshot, default));
        Assert.Equal(before, (await inbox.DetailAsync(snapshot[0], default)).Data!.ReadAt);
        Assert.Equal(1, (await inbox.SummaryAsync(default)).Data!.UnreadCount);
    }

    [Fact]
    public async Task Inbox_ForeignBatchRejectedAtomicallyAndUnresolvedFailsClosed()
    {
        var (tenant, db) = Create(); await using var owned = db; var recipient = await Seed(db, tenant);
        await Deliver(db, tenant, "scope", [tenant.UserId!.Value, recipient]);
        var own = new NotificationInboxService(db, tenant, TimeProvider.System);
        var all = await db.NotificationDeliveries.ToListAsync();
        var ownId = all.Single(x => x.UserId == tenant.UserId).DeliveryId.ToString();
        var foreignId = all.Single(x => x.UserId == recipient).DeliveryId.ToString();
        Assert.Equal(ErrorCodes.ResourceNotFound, await own.ReadAsync([ownId, foreignId], default));
        Assert.All(all, x => Assert.Null(x.ReadAt));
        Assert.Equal(ErrorCodes.ResourceNotFound, (await own.DetailAsync(foreignId, default)).ErrorCode);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await new NotificationInboxService(db, new TenantContext(), TimeProvider.System).SummaryAsync(default)).ErrorCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData(" 1")]
    [InlineData("18446744073709551616")]
    public async Task Inbox_RejectsMalformedId(string value)
    {
        var (tenant, db) = Create(); await using var owned = db;
        Assert.Equal(ErrorCodes.ValidationFailed, (await new NotificationInboxService(db, tenant, TimeProvider.System).DetailAsync(value, default)).ErrorCode);
    }

    [Fact]
    public async Task Inbox_RejectsInvalidPageOrDuplicateBatch()
    {
        var (tenant, db) = Create(); await using var owned = db;
        var inbox = new NotificationInboxService(db, tenant, TimeProvider.System);
        foreach (var query in new[] { new NotificationListQuery { Page = 0 }, new() { PageSize = 101 }, new() { Page = int.MaxValue } })
            Assert.Equal(ErrorCodes.ValidationFailed, (await inbox.ListAsync(query, default)).ErrorCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await inbox.ReadAsync(["1", "1"], default));
    }

    private static (TenantContext, EduTwinDbContext) Create()
    {
        var tenant = new TenantContext(); tenant.Initialize(Guid.NewGuid(), Guid.NewGuid(), "Teacher", 1);
        return (tenant, new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant));
    }
    internal static async Task<Guid> Seed(EduTwinDbContext db, TenantContext tenant,
        UserRole senderRole = UserRole.Teacher, UserRole recipientRole = UserRole.Teacher)
    {
        var center = tenant.CenterId!.Value;
        var now = DateTime.UtcNow;
        db.Centers.Add(new Center { CenterId = center, CenterCode = "NOTIFY_TEST", CenterName = "Isolated notification test", Timezone = "Asia/Ho_Chi_Minh", Status = CenterStatus.Active, CreatedAt = now, UpdatedAt = now });
        var recipient = Guid.NewGuid();
        foreach (var id in new[] { tenant.UserId!.Value, recipient })
            db.Users.Add(new User { UserId = id, CenterId = center, Username = "notify_" + id.ToString("N"), DisplayName = "Isolated test user", PasswordHash = "not-a-password", RoleName = id == tenant.UserId ? senderRole : recipientRole, Status = UserStatus.Active, AuthVersion = 1, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync(); return recipient;
    }
    private static async Task Deliver(EduTwinDbContext db, TenantContext tenant, string key, Guid[]? ids = null)
    {
        var eventId = await new NotificationOutbox(db, tenant, TimeProvider.System).StageNoticeAsync(key, "Thông báo " + key, "Nội dung kiểm thử", ids ?? [tenant.UserId!.Value]);
        await db.SaveChangesAsync(); Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(eventId));
    }
}
