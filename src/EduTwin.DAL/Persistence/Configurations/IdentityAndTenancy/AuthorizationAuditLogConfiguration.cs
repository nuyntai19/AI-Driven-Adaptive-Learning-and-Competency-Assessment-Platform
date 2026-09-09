using EduTwin.DAL.IdentityAndTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Authorization;

public sealed class AuthorizationAuditLogConfiguration : IEntityTypeConfiguration<AuthorizationAuditLog>
{
    public void Configure(EntityTypeBuilder<AuthorizationAuditLog> builder)
    {
        builder.ToTable("authorization_audit_logs");
        builder.HasKey(x => x.AuthorizationAuditId).HasName("pk_authorization_audit_logs");
        builder.Property(x => x.AuthorizationAuditId).HasColumnName("authorization_audit_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id").HasColumnType("varchar(36)");
        builder.Property(x => x.ActionType).HasColumnName("action_type").HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.TargetType).HasColumnName("target_type").HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.TargetId).HasColumnName("target_id").HasColumnType("varchar(128)").IsRequired();
        builder.Property(x => x.TargetUserId).HasColumnName("target_user_id").HasColumnType("varchar(36)");
        builder.Property(x => x.PermissionCode).HasColumnName("permission_code").HasColumnType("varchar(100)");
        builder.Property(x => x.BeforeData).HasColumnName("before_data").HasColumnType("json");
        builder.Property(x => x.AfterData).HasColumnName("after_data").HasColumnType("json");
        builder.Property(x => x.Reason).HasColumnName("reason").HasColumnType("varchar(1000)").IsRequired();
        builder.Property(x => x.TraceId).HasColumnName("trace_id").HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.HasAlternateKey(x => new { x.CenterId, x.AuthorizationAuditId })
            .HasName("ux_authorization_audit_logs_center_id_audit_id");
        builder.HasIndex(x => new { x.CenterId, x.CreatedAt, x.ActionType })
            .HasDatabaseName("ix_authorization_audit_logs_center_created_action");
        builder.HasIndex(x => new { x.CenterId, x.ActorUserId, x.CreatedAt })
            .HasDatabaseName("ix_authorization_audit_logs_center_actor_created");
        builder.HasIndex(x => new { x.CenterId, x.TargetUserId, x.CreatedAt })
            .HasDatabaseName("ix_authorization_audit_logs_center_target_created");
        builder.HasOne(x => x.ActorUser).WithMany()
            .HasForeignKey(x => new { x.CenterId, x.ActorUserId })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_authorization_audit_logs_users_actor");
        builder.HasOne(x => x.TargetUser).WithMany()
            .HasForeignKey(x => new { x.CenterId, x.TargetUserId })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_authorization_audit_logs_users_target");
    }
}
