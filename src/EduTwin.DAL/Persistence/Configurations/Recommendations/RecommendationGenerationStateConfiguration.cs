using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public sealed class RecommendationGenerationStateConfiguration
    : IEntityTypeConfiguration<RecommendationGenerationState>
{
    public void Configure(EntityTypeBuilder<RecommendationGenerationState> builder)
    {
        builder.ToTable("recommendation_generation_states");

        builder.HasKey(x => new { x.CenterId, x.StudentId, x.SubjectId })
            .HasName("pk_recommendation_generation_states");

        builder.Property(x => x.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)");
        builder.Property(x => x.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("varchar(36)");
        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)");
        builder.Property(x => x.LastTriggerAt)
            .HasColumnName("last_trigger_at")
            .HasColumnType("datetime(6)")
            .IsRequired();
        builder.Property(x => x.LastSourceAttemptId)
            .HasColumnName("last_source_attempt_id")
            .HasColumnType("bigint unsigned");
        builder.Property(x => x.LastOutcome)
            .HasColumnName("last_outcome")
            .HasColumnType("varchar(32)")
            .IsRequired();
        builder.Property(x => x.DiagnosticReason)
            .HasColumnName("diagnostic_reason")
            .HasColumnType("varchar(500)");
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();
        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .HasDefaultValue(1ul)
            .IsConcurrencyToken();

        builder.HasIndex(x => new { x.CenterId, x.SubjectId })
            .HasDatabaseName("ix_recommendation_generation_states_center_id_subject_id");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_recommendation_generation_states_outcome",
            "`last_outcome` IN ('Generated', 'NoCandidate', 'Blocked')"));

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => new { x.CenterId, x.StudentId })
            .HasPrincipalKey(x => new { x.CenterId, x.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recommendation_generation_states_students_student");

        builder.HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => new { x.CenterId, x.SubjectId })
            .HasPrincipalKey(x => new { x.CenterId, x.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recommendation_generation_states_subjects_subject");
    }
}
