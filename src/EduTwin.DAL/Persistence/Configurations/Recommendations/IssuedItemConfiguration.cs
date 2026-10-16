using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class IssuedItemConfiguration : IEntityTypeConfiguration<IssuedItem>
{
    public void Configure(EntityTypeBuilder<IssuedItem> builder)
    {
        builder.ToTable("issued_items");

        builder.HasKey(i => i.IssuedItemId).HasName("pk_issued_items");

        builder.HasAlternateKey(i => new { i.CenterId, i.IssuedItemId })
            .HasName("ux_issued_items_center_id_issued_item_id");

        builder.HasIndex(i => new { i.CenterId, i.ActivityRunId, i.ItemOrder })
            .HasDatabaseName("ix_issued_items_center_activity_run_order");

        builder.Property(i => i.IssuedItemId)
            .HasColumnName("issued_item_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(i => i.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(i => i.ActivityRunId)
            .HasColumnName("activity_run_id")
            .HasColumnType("bigint unsigned")
            .IsRequired();

        builder.Property(i => i.QuestionId)
            .HasColumnName("question_id")
            .HasColumnType("bigint unsigned")
            .IsRequired(false);

        builder.Property(i => i.ItemOrder)
            .HasColumnName("item_order")
            .HasColumnType("int unsigned")
            .HasDefaultValue(1u)
            .IsRequired();

        builder.Property(i => i.ExposureMode)
            .HasColumnName("exposure_mode")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Interactive")
            .IsRequired();

        builder.Property(i => i.IssuedAt)
            .HasColumnName("issued_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(i => i.AttemptId)
            .HasColumnName("attempt_id")
            .HasColumnType("bigint unsigned")
            .IsRequired(false);

        builder.Property(i => i.SolutionExposedAt)
            .HasColumnName("solution_exposed_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(i => i.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(i => i.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(i => i.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(i => i.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(i => i.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(i => i.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .IsConcurrencyToken()
            .HasDefaultValue(1ul)
            .IsRequired();

        builder.HasOne(i => i.ActivityRun)
            .WithMany(a => a.IssuedItems)
            .HasForeignKey(i => new { i.CenterId, i.ActivityRunId })
            .HasPrincipalKey(a => new { a.CenterId, a.ActivityRunId })
            .HasConstraintName("fk_issued_items_activity_runs_activity_run")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Attempt)
            .WithMany()
            .HasForeignKey(i => new { i.CenterId, i.AttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AttemptId })
            .HasConstraintName("fk_issued_items_attempts_attempt")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
