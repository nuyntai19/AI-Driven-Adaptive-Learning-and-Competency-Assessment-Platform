using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class PathDecisionConfiguration : IEntityTypeConfiguration<PathDecision>
{
    public void Configure(EntityTypeBuilder<PathDecision> builder)
    {
        builder.ToTable("path_decisions");

        builder.HasKey(d => d.DecisionId).HasName("pk_path_decisions");

        builder.HasAlternateKey(d => new { d.CenterId, d.DecisionId })
            .HasName("ux_path_decisions_center_id_decision_id");

        builder.HasIndex(d => new { d.CenterId, d.LearningPathId, d.CreatedAt })
            .HasDatabaseName("ix_path_decisions_center_learning_path_created_at");

        builder.Property(d => d.DecisionId)
            .HasColumnName("decision_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(d => d.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(d => d.LearningPathId)
            .HasColumnName("learning_path_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(d => d.SessionRunId)
            .HasColumnName("session_run_id")
            .HasColumnType("bigint unsigned")
            .IsRequired(false);

        builder.Property(d => d.TriggerType)
            .HasColumnName("trigger_type")
            .HasColumnType("varchar(64)")
            .IsRequired();

        builder.Property(d => d.ReasonCodes)
            .HasColumnName("reason_codes")
            .HasColumnType("json")
            .HasConversion(
                v => v.RootElement.GetRawText(),
                v => JsonDocument.Parse(v, default))
            .IsRequired();

        builder.Property(d => d.EvidenceReferenceIds)
            .HasColumnName("evidence_reference_ids")
            .HasColumnType("json")
            .HasConversion(
                v => v != null ? v.RootElement.GetRawText() : null,
                v => v != null ? JsonDocument.Parse(v, default) : null)
            .IsRequired(false);

        builder.Property(d => d.DecisionSummary)
            .HasColumnName("decision_summary")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(d => d.PolicyVersion)
            .HasColumnName("policy_version")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("v4-pilot-1")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(d => d.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(d => d.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(d => d.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(d => d.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(d => d.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(d => d.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .IsConcurrencyToken()
            .HasDefaultValue(1ul)
            .IsRequired();

        builder.HasOne(d => d.LearningPath)
            .WithMany()
            .HasForeignKey(d => new { d.CenterId, d.LearningPathId })
            .HasPrincipalKey(p => new { p.CenterId, p.LearningPathId })
            .HasConstraintName("fk_path_decisions_learning_paths_learning_path")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
