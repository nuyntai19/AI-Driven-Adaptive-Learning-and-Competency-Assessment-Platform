using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Notifications;

public sealed class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> b)
    {
        b.ToTable("notification_events");
        b.HasKey(x => x.EventId);
        b.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("varchar(36)");
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.DeduplicationKey).HasColumnName("deduplication_key").HasColumnType("varchar(160)");
        b.Property(x => x.EventType).HasColumnName("event_type").HasColumnType("varchar(64)");
        b.Property(x => x.Title).HasColumnName("title").HasColumnType("varchar(160)");
        b.Property(x => x.Body).HasColumnName("body").HasColumnType("varchar(2000)");
        b.Property(x => x.RecipientUserIds).HasColumnName("recipient_user_ids").HasColumnType("json");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.Property(x => x.MessageId).HasColumnName("message_id").HasColumnType("varchar(36)");
        b.Property(x => x.SenderName).HasColumnName("sender_name").HasColumnType("varchar(160)");
        b.Property(x => x.ResourceContext).HasColumnName("resource_context").HasColumnType("json");
        b.HasOne<NotificationMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
        b.HasAlternateKey(x => new { x.CenterId, x.EventId });
        b.HasIndex(x => new { x.CenterId, x.DeduplicationKey }).IsUnique();
        b.HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => new { x.CenterId, x.CreatedBy })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> b)
    {
        b.ToTable("notification_messages");
        b.HasKey(x => x.MessageId);
        b.Property(x => x.MessageId).HasColumnName("message_id").HasColumnType("varchar(36)");
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.SenderUserId).HasColumnName("sender_user_id").HasColumnType("varchar(36)");
        b.Property(x => x.ClientMessageId).HasColumnName("client_message_id").HasColumnType("varchar(36)");
        b.Property(x => x.SenderName).HasColumnName("sender_name").HasColumnType("varchar(160)");
        b.Property(x => x.SenderAuthVersion).HasColumnName("sender_auth_version");
        b.Property(x => x.Title).HasColumnName("title").HasColumnType("varchar(160)");
        b.Property(x => x.Body).HasColumnName("body").HasColumnType("varchar(2000)");
        b.Property(x => x.Audience).HasColumnName("audience").HasColumnType("json");
        b.Property(x => x.Priority).HasColumnName("priority").HasColumnType("varchar(16)");
        b.Property(x => x.Category).HasColumnName("category").HasColumnType("varchar(32)");
        b.Property(x => x.RequiresAcknowledgement).HasColumnName("requires_acknowledgement");
        b.Property(x => x.AllowReply).HasColumnName("allow_reply");
        b.Property(x => x.CorrectionOfMessageId).HasColumnName("correction_of_message_id").HasColumnType("varchar(36)");
        b.HasOne<NotificationMessage>().WithMany().HasForeignKey(x => new { x.CenterId, x.CorrectionOfMessageId })
            .HasPrincipalKey(x => new { x.CenterId, x.MessageId }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Fingerprint).HasColumnName("fingerprint").HasColumnType("varchar(64)");
        b.Property(x => x.RecipientCount).HasColumnName("recipient_count");
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.HasAlternateKey(x => new { x.CenterId, x.MessageId });
        b.HasIndex(x => new { x.CenterId, x.SenderUserId, x.ClientMessageId }).IsUnique();
        b.HasIndex(x => new { x.CenterId, x.SenderUserId, x.CreatedAt });
        b.HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => new { x.CenterId, x.SenderUserId })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> b)
    {
        b.ToTable("notification_dispatches", t => t.HasCheckConstraint("ck_notification_dispatch_state",
            "`state` IN ('Pending','Processing','Completed','DeadLetter')"));
        b.HasKey(x => new { x.CenterId, x.EventId });
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("varchar(36)");
        b.Property(x => x.State).HasColumnName("state").HasColumnType("varchar(32)");
        b.Property(x => x.AvailableAt).HasColumnName("available_at").HasColumnType("datetime(6)");
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until").HasColumnType("datetime(6)");
        b.Property(x => x.LeaseToken).HasColumnName("lease_token").HasColumnType("varchar(36)");
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        b.Property(x => x.DeliveredCount).HasColumnName("delivered_count");
        b.Property(x => x.SuppressedCount).HasColumnName("suppressed_count");
        b.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasColumnType("varchar(64)");
        b.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").IsConcurrencyToken();
        b.HasOne<NotificationEvent>().WithOne().HasForeignKey<NotificationDispatch>(x => new { x.CenterId, x.EventId })
            .HasPrincipalKey<NotificationEvent>(x => new { x.CenterId, x.EventId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.State, x.AvailableAt, x.CenterId });
    }
}

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> b)
    {
        b.ToTable("notification_deliveries");
        b.HasKey(x => x.DeliveryId);
        b.Property(x => x.DeliveryId).HasColumnName("delivery_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.EventId).HasColumnName("event_id").HasColumnType("varchar(36)");
        b.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("varchar(36)");
        b.Property(x => x.DeliveredAt).HasColumnName("delivered_at").HasColumnType("datetime(6)");
        b.Property(x => x.ReadAt).HasColumnName("read_at").HasColumnType("datetime(6)");
        b.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at").HasColumnType("datetime(6)");
        b.Property(x => x.HiddenAt).HasColumnName("hidden_at").HasColumnType("datetime(6)");
        b.HasAlternateKey(x => new { x.CenterId, x.DeliveryId });
        b.HasOne(x => x.Event).WithMany().HasForeignKey(x => new { x.CenterId, x.EventId })
            .HasPrincipalKey(x => new { x.CenterId, x.EventId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => new { x.CenterId, x.UserId })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CenterId, x.EventId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.CenterId, x.UserId, x.ReadAt, x.DeliveryId });
    }
}

public sealed class NotificationMessageActionConfiguration : IEntityTypeConfiguration<NotificationMessageAction>
{
    public void Configure(EntityTypeBuilder<NotificationMessageAction> b)
    {
        b.ToTable("notification_message_actions", t => t.HasCheckConstraint("ck_notification_message_action_kind",
            "`kind` IN ('Retracted','Corrected') AND ((`kind` = 'Corrected' AND `replacement_message_id` IS NOT NULL) OR (`kind` = 'Retracted' AND `replacement_message_id` IS NULL))"));
        b.HasKey(x => x.ActionId);
        b.Property(x => x.ActionId).HasColumnName("action_id").HasColumnType("varchar(36)");
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.MessageId).HasColumnName("message_id").HasColumnType("varchar(36)");
        b.Property(x => x.ClientActionId).HasColumnName("client_action_id").HasColumnType("varchar(36)");
        b.Property(x => x.ReplacementMessageId).HasColumnName("replacement_message_id").HasColumnType("varchar(36)");
        b.Property(x => x.Kind).HasColumnName("kind").HasColumnType("varchar(16)");
        b.Property(x => x.Reason).HasColumnName("reason").HasColumnType("varchar(500)");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.HasIndex(x => new { x.CenterId, x.MessageId }).IsUnique();
        b.HasIndex(x => new { x.CenterId, x.CreatedBy, x.ClientActionId }).IsUnique();
        b.HasOne<NotificationMessage>().WithMany().HasForeignKey(x => new { x.CenterId, x.MessageId })
            .HasPrincipalKey(x => new { x.CenterId, x.MessageId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<NotificationMessage>().WithMany().HasForeignKey(x => new { x.CenterId, x.ReplacementMessageId })
            .HasPrincipalKey(x => new { x.CenterId, x.MessageId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => new { x.CenterId, x.CreatedBy })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class NotificationReplyConfiguration : IEntityTypeConfiguration<NotificationReply>
{
    public void Configure(EntityTypeBuilder<NotificationReply> b)
    {
        b.ToTable("notification_replies");
        b.HasKey(x => x.ReplyId);
        b.Property(x => x.ReplyId).HasColumnName("reply_id").HasColumnType("varchar(36)");
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.DeliveryId).HasColumnName("delivery_id").HasColumnType("bigint unsigned");
        b.Property(x => x.ClientReplyId).HasColumnName("client_reply_id").HasColumnType("varchar(36)");
        b.Property(x => x.Body).HasColumnName("body").HasColumnType("varchar(2000)");
        b.Property(x => x.SenderName).HasColumnName("sender_name").HasColumnType("varchar(160)");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.HasIndex(x => new { x.CenterId, x.DeliveryId }).IsUnique();
        b.HasOne<NotificationDelivery>().WithMany().HasForeignKey(x => new { x.CenterId, x.DeliveryId })
            .HasPrincipalKey(x => new { x.CenterId, x.DeliveryId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => new { x.CenterId, x.CreatedBy })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
