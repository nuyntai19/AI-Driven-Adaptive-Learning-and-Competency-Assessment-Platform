using System.Text.Json;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed partial class NotificationMySqlTests
{
    [NotificationMySqlFact]
    public async Task UpgradeLifecycleCopy_PreservesAcademicRowsAndHistory_NoRetrospectiveEvents()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student, "20261010092545_AddNotificationLifecycle");
        var data = await Academic(f, 1);
        await using var db = f.Context();
        var eventId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO notification_events(event_id,center_id,deduplication_key,event_type,title,body,recipient_user_ids,created_at,created_by,sender_name) VALUES ({eventId.ToString()},{f.CenterId.ToString()},'isolated-upgrade','SystemNotice','Isolated historical notice','Unchanged historical body',{JsonSerializer.Serialize(new[] { f.UserId })},{DateTime.UtcNow},{f.UserId.ToString()},'Isolated sender')");
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(data.AssignmentId, (await db.Assignments.SingleAsync()).AssignmentId);
        Assert.Equal("Isolated", (await db.Questions.SingleAsync()).QuestionText);
        Assert.Single(await db.StudentAssignmentProgresses.ToArrayAsync());
        var history = Assert.Single(await db.NotificationEvents.ToArrayAsync());
        Assert.Equal(eventId, history.EventId); Assert.Equal("Unchanged historical body", history.Body); Assert.Null(history.ResourceContext);
        Assert.Empty(await db.NotificationDeliveries.ToArrayAsync());
        Assert.Contains("20261010101422_AddNotificationOperations", await db.Database.GetAppliedMigrationsAsync());
        // Destructive rollback is deliberately not used: this gate proves additive upgrade only.
    }

    [NotificationMySqlFact]
    public async Task IndexesAndExplain_InboxUnreadAndEligibleQueue_RealSql()
    {
        await using var f = await Fixture.Create();
        await using var connection = new MySqlConnection(f.Connection); await connection.OpenAsync();
        async Task<string> Scalar(string sql)
        {
            await using var command = connection.CreateCommand(); command.CommandText = sql;
            command.Parameters.AddWithValue("@center", f.CenterId.ToString()); command.Parameters.AddWithValue("@user", f.UserId.ToString());
            return Convert.ToString(await command.ExecuteScalarAsync())!;
        }
        var deliveryIndex = await Scalar("SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='notification_deliveries' AND INDEX_NAME='IX_notification_deliveries_center_id_user_id_read_at_delivery_id'");
        var queueIndex = await Scalar("SELECT GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='notification_dispatches' AND INDEX_NAME='IX_notification_dispatches_state_available_at_center_id'");
        Assert.Equal("center_id,user_id,read_at,delivery_id", deliveryIndex);
        Assert.Equal("state,available_at,center_id", queueIndex);
        var inbox = await Scalar("EXPLAIN FORMAT=JSON SELECT delivery_id FROM notification_deliveries WHERE center_id=@center AND user_id=@user AND read_at IS NULL ORDER BY delivery_id DESC LIMIT 20");
        var queue = await Scalar("EXPLAIN FORMAT=JSON SELECT center_id,event_id FROM notification_dispatches WHERE state='Pending' AND available_at<=UTC_TIMESTAMP(6) ORDER BY available_at LIMIT 20");
        Assert.Contains("IX_notification_deliveries_center_id_user_id_read_at_delivery_id", inbox);
        Assert.Contains("IX_notification_dispatches_state_available_at_center_id", queue);
        output.WriteLine("NOTIFICATION_INDEX_PROOF " + JsonSerializer.Serialize(new { deliveryIndex, queueIndex, inboxExplain = JsonDocument.Parse(inbox).RootElement, queueExplain = JsonDocument.Parse(queue).RootElement }));
    }
}
