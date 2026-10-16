using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class ActivityRunConfiguration : IEntityTypeConfiguration<ActivityRun>
{
    public void Configure(EntityTypeBuilder<ActivityRun> builder)
    {
        builder.ToTable("activity_runs");

        builder.HasKey(a => a.ActivityRunId).HasName("pk_activity_runs");

        builder.HasAlternateKey(a => new { a.CenterId, a.ActivityRunId })
            .HasName("ux_activity_runs_center_id_activity_run_id");

        builder.HasIndex(a => new { a.CenterId, a.SessionRunId, a.ActivityOrder })
            .HasDatabaseName("ix_activity_runs_center_session_run_order");

        builder.Property(a => a.ActivityRunId)
            .HasColumnName("activity_run_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(a => a.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(a => a.SessionRunId)
            .HasColumnName("session_run_id")
            .HasColumnType("bigint unsigned")
            .IsRequired();

        builder.Property(a => a.ContentVersionId)
            .HasColumnName("content_version_id")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(a => a.ActivityType)
            .HasColumnName("activity_type")
            .HasColumnType("varchar(32)")
            .IsRequired();

        builder.Property(a => a.ActivityOrder)
            .HasColumnName("activity_order")
            .HasColumnType("int unsigned")
            .HasDefaultValue(1u)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("NotStarted")
            .IsRequired();

        builder.Property(a => a.DraftPayload)
            .HasColumnName("draft_payload")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(a => a.DraftRevision)
            .HasColumnName("draft_revision")
            .HasColumnType("int unsigned")
            .HasDefaultValue(1u)
            .IsRequired();

        builder.Property(a => a.AssistanceRequestedCount)
            .HasColumnName("assistance_requested_count")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u)
            .IsRequired();

        builder.Property(a => a.AssistanceMode)
            .HasColumnName("assistance_mode")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Normal")
            .IsRequired();

        builder.Property(a => a.Score)
            .HasColumnName("score")
            .HasColumnType("decimal(6,2)")
            .IsRequired(false);

        builder.Property(a => a.MaxScore)
            .HasColumnName("max_score")
            .HasColumnType("decimal(6,2)")
            .IsRequired(false);

        builder.Property(a => a.IsMastered)
            .HasColumnName("is_mastered")
            .HasColumnType("tinyint(1)")
            .IsRequired(false);

        builder.Property(a => a.Feedback)
            .HasColumnName("feedback")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(a => a.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(a => a.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(a => a.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(a => a.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(a => a.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(a => a.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(a => a.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .IsConcurrencyToken()
            .HasDefaultValue(1ul)
            .IsRequired();

        builder.HasOne(a => a.SessionRun)
            .WithMany()
            .HasForeignKey(a => new { a.CenterId, a.SessionRunId })
            .HasPrincipalKey(r => new { r.CenterId, r.SessionRunId })
            .HasConstraintName("fk_activity_runs_session_runs_session_run")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ContentVersion)
            .WithMany()
            .HasForeignKey(a => new { a.CenterId, a.ContentVersionId })
            .HasPrincipalKey(c => new { c.CenterId, c.ContentVersionId })
            .HasConstraintName("fk_activity_runs_content_versions_content_version")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
