using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public class ReasoningAnalysisConfiguration : IEntityTypeConfiguration<ReasoningAnalysis>
{
    public void Configure(EntityTypeBuilder<ReasoningAnalysis> builder)
    {
        builder.ToTable("reasoning_analyses");

        builder.HasKey(r => r.AnalysisId).HasName("pk_reasoning_analyses");

        builder.HasAlternateKey(r => new { r.CenterId, r.AnalysisId })
            .HasName("ux_reasoning_analyses_center_id_analysis_id");

        builder.HasIndex(r => new { r.CenterId, r.AttemptId })
            .IsUnique()
            .HasDatabaseName("ux_reasoning_analyses_center_id_attempt_id");

        builder.HasIndex(r => new { r.CenterId, r.NeedsTeacherReview, r.CreatedAt })
            .HasDatabaseName("ix_reasoning_analyses_center_id_needs_teacher_review_created_at");

        builder.HasIndex(r => new { r.CenterId, r.OverriddenByTeacherId })
            .HasDatabaseName("ix_reasoning_analyses_center_id_overridden_by_teacher_id");

        builder.Property(r => r.AnalysisId).HasColumnName("analysis_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(r => r.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(r => r.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned");
        builder.Property(r => r.SchemaVersion).HasColumnName("schema_version").HasColumnType("varchar(20)").IsRequired();
        builder.Property(r => r.MethodDetected).HasColumnName("method_detected").HasColumnType("varchar(500)");
        builder.Property(r => r.ReasoningQuality).HasColumnName("reasoning_quality").HasColumnType("decimal(5,2)");

        builder.Property(r => r.ErrorType)
            .HasColumnName("error_type")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.Misconception).HasColumnName("misconception").HasColumnType("varchar(1000)");

        builder.Property(r => r.MissingSteps)
            .HasColumnName("missing_steps")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(
                v => v.RootElement.ToString(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));

        builder.Property(r => r.RootCauseNodeIds)
            .HasColumnName("root_cause_node_ids")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(
                v => v.RootElement.ToString(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));

        builder.Property(r => r.AnalysisConfidence).HasColumnName("analysis_confidence").HasColumnType("decimal(5,2)");
        builder.Property(r => r.Feedback).HasColumnName("feedback").HasColumnType("longtext").IsRequired();
        builder.Property(r => r.IsFallback).HasColumnName("is_fallback").HasColumnType("tinyint(1)");
        builder.Property(r => r.NeedsTeacherReview).HasColumnName("needs_teacher_review").HasColumnType("tinyint(1)");

        builder.Property(r => r.Provider)
            .HasColumnName("provider")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.ModelName).HasColumnName("model_name").HasColumnType("varchar(100)");
        builder.Property(r => r.OverrideReasoningQuality).HasColumnName("override_reasoning_quality").HasColumnType("decimal(5,2)");

        builder.Property(r => r.OverrideErrorType)
            .HasColumnName("override_error_type")
            .HasColumnType("varchar(32)")
            .HasConversion<string>();

        builder.Property(r => r.OverrideFeedback).HasColumnName("override_feedback").HasColumnType("longtext");
        builder.Property(r => r.OverrideIsCorrect).HasColumnName("override_is_correct").HasColumnType("tinyint(1)");
        builder.Property(r => r.OverrideReason).HasColumnName("override_reason").HasColumnType("varchar(1000)");
        builder.Property(r => r.OverriddenByTeacherId).HasColumnName("overridden_by_teacher_id").HasColumnType("varchar(36)");
        builder.Property(r => r.OverriddenAt).HasColumnName("overridden_at").HasColumnType("datetime(6)");
        builder.Property(r => r.OverrideVersion).HasColumnName("override_version").HasColumnType("int unsigned").HasDefaultValue(0u);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(r => r.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_reasoning_analyses_reasoning_quality", "`reasoning_quality` IS NULL OR `reasoning_quality` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_reasoning_analyses_analysis_confidence", "`analysis_confidence` IS NULL OR `analysis_confidence` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_reasoning_analyses_override_reasoning_quality", "`override_reasoning_quality` IS NULL OR `override_reasoning_quality` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_reasoning_analyses_error_type", "`error_type` IN ('None', 'Knowledge', 'Skill', 'Reasoning', 'Behavior', 'Presentation', 'Unknown')");
            t.HasCheckConstraint("ck_reasoning_analyses_override_error_type", "`override_error_type` IS NULL OR `override_error_type` IN ('None', 'Knowledge', 'Skill', 'Reasoning', 'Behavior', 'Presentation', 'Unknown')");
            t.HasCheckConstraint("ck_reasoning_analyses_provider", "`provider` IN ('Gemini', 'RuleBased')");
        });

        builder.HasOne(r => r.Attempt)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.AttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_reasoning_analyses_attempts_attempt");

        builder.HasOne(r => r.OverriddenByTeacher)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.OverriddenByTeacherId })
            .HasPrincipalKey(t => new { t.CenterId, t.TeacherId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_reasoning_analyses_teachers_overridden_by_teacher");
    }
}
