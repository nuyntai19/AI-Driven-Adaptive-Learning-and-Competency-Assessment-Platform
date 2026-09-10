using System.Text.Json;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public sealed class EvidenceAssessmentConfiguration : IEntityTypeConfiguration<EvidenceAssessment>
{
    public void Configure(EntityTypeBuilder<EvidenceAssessment> builder)
    {
        builder.ToTable("evidence_assessments");
        builder.HasKey(e => e.EvidenceAssessmentId).HasName("pk_evidence_assessments");

        builder.HasAlternateKey(e => new { e.CenterId, e.EvidenceAssessmentId })
            .HasName("ux_evidence_center_assessment");
        builder.HasAlternateKey(e => new { e.CenterId, e.EvidenceAssessmentId, e.AttemptId })
            .HasName("ux_evidence_center_assessment_attempt");

        builder.HasIndex(e => new { e.CenterId, e.AttemptId, e.EvaluatedAt })
            .HasDatabaseName("ix_evidence_center_attempt_evaluated");
        builder.HasIndex(e => new { e.CenterId, e.RequiresTeacherReview, e.EvaluatedAt })
            .HasDatabaseName("ix_evidence_center_review_evaluated");
        builder.HasIndex(e => new { e.CenterId, e.AnalysisId, e.AttemptId })
            .HasDatabaseName("ix_evidence_center_analysis_attempt");
        builder.HasIndex(e => new { e.CenterId, e.SupersedesAssessmentId, e.AttemptId })
            .HasDatabaseName("ix_evidence_center_supersedes_attempt");

        builder.Property(e => e.EvidenceAssessmentId).HasColumnName("evidence_assessment_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(e => e.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(e => e.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(e => e.AnalysisId).HasColumnName("analysis_id").HasColumnType("bigint unsigned");
        builder.Property(e => e.SupersedesAssessmentId).HasColumnName("supersedes_assessment_id").HasColumnType("bigint unsigned");
        builder.Property(e => e.SourceType).HasColumnName("source_type").HasColumnType("varchar(32)").HasConversion<string>().IsRequired();
        builder.Property(e => e.TrustLevel).HasColumnName("trust_level").HasColumnType("varchar(32)").HasConversion<string>().IsRequired();
        builder.Property(e => e.DecisionMode).HasColumnName("decision_mode").HasColumnType("varchar(32)").HasConversion<string>().IsRequired();
        builder.Property(e => e.ReasoningWeight).HasColumnName("reasoning_weight").HasColumnType("decimal(4,3)").IsRequired();
        builder.Property(e => e.ReasonCodes)
            .HasColumnName("reason_codes")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(v => v.RootElement.ToString(), v => JsonDocument.Parse(v, new JsonDocumentOptions()));
        builder.Property(e => e.RequiresTeacherReview).HasColumnName("requires_teacher_review").HasColumnType("tinyint(1)").IsRequired();
        builder.Property(e => e.PolicyVersion).HasColumnName("policy_version").HasColumnType("varchar(32)").IsRequired();
        builder.Property(e => e.AnalysisOverrideVersion).HasColumnName("analysis_override_version").HasColumnType("int unsigned").IsRequired();
        builder.Property(e => e.EvaluatedAt).HasColumnName("evaluated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_evidence_assessments_reasoning_weight", "`reasoning_weight` BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_evidence_assessments_source_type", "`source_type` IN ('AI', 'RuleFallback', 'TeacherOverride')");
            t.HasCheckConstraint("ck_evidence_assessments_trust_level", "`trust_level` IN ('Trusted', 'Reduced', 'ReviewOnly')");
            t.HasCheckConstraint("ck_evidence_assessments_decision_mode", "`decision_mode` IN ('AIWeighted', 'DeterministicOnly', 'HumanConfirmed')");
        });

        builder.HasOne(e => e.Attempt)
            .WithMany()
            .HasForeignKey(e => new { e.CenterId, e.AttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_evidence_assessments_attempts_attempt");

        builder.HasOne(e => e.Analysis)
            .WithMany()
            .HasForeignKey(e => new { e.CenterId, e.AnalysisId, e.AttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AnalysisId, a.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_evidence_assessments_reasoning_analyses_analysis");

        builder.HasOne(e => e.SupersedesAssessment)
            .WithMany()
            .HasForeignKey(e => new { e.CenterId, e.SupersedesAssessmentId, e.AttemptId })
            .HasPrincipalKey(e => new { e.CenterId, e.EvidenceAssessmentId, e.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_evidence_assessments_evidence_assessments_supersedes");
    }
}
