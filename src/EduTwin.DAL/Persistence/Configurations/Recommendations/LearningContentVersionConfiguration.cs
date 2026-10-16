using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class LearningContentVersionConfiguration : IEntityTypeConfiguration<LearningContentVersion>
{
    public void Configure(EntityTypeBuilder<LearningContentVersion> builder)
    {
        builder.ToTable("learning_content_versions");

        builder.HasKey(c => c.ContentVersionId).HasName("pk_learning_content_versions");

        builder.HasAlternateKey(c => new { c.CenterId, c.ContentVersionId })
            .HasName("ux_learning_content_versions_center_id_content_version_id");

        builder.HasIndex(c => new { c.CenterId, c.SubjectId, c.SkillId, c.Status })
            .HasDatabaseName("ix_learning_content_versions_center_subject_skill_status");

        builder.Property(c => c.ContentVersionId)
            .HasColumnName("content_version_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(c => c.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(c => c.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(c => c.SkillId)
            .HasColumnName("skill_id")
            .HasColumnType("varchar(64)")
            .IsRequired();

        builder.Property(c => c.ActivityType)
            .HasColumnName("activity_type")
            .HasColumnType("varchar(32)")
            .IsRequired();

        builder.Property(c => c.Title)
            .HasColumnName("title")
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(c => c.EstimatedDurationMinutes)
            .HasColumnName("estimated_duration_minutes")
            .HasColumnType("int unsigned")
            .HasDefaultValue(15u)
            .IsRequired();

        builder.Property(c => c.Payload)
            .HasColumnName("payload")
            .HasColumnType("json")
            .HasConversion(
                v => v.RootElement.GetRawText(),
                v => JsonDocument.Parse(v, default))
            .IsRequired();

        builder.Property(c => c.Rubric)
            .HasColumnName("rubric")
            .HasColumnType("json")
            .HasConversion(
                v => v != null ? v.RootElement.GetRawText() : null,
                v => v != null ? JsonDocument.Parse(v, default) : null)
            .IsRequired(false);

        builder.Property(c => c.Prerequisites)
            .HasColumnName("prerequisites")
            .HasColumnType("json")
            .HasConversion(
                v => v != null ? v.RootElement.GetRawText() : null,
                v => v != null ? JsonDocument.Parse(v, default) : null)
            .IsRequired(false);

        builder.Property(c => c.Version)
            .HasColumnName("version")
            .HasColumnType("int unsigned")
            .HasDefaultValue(1u)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Published")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(c => c.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(c => c.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(c => c.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .IsConcurrencyToken()
            .HasDefaultValue(1ul)
            .IsRequired();

        builder.HasOne(c => c.Subject)
            .WithMany()
            .HasForeignKey(c => new { c.CenterId, c.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .HasConstraintName("fk_learning_content_versions_subjects_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
