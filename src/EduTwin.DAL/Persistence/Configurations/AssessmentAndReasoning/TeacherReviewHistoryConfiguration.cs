using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public class TeacherReviewHistoryConfiguration : IEntityTypeConfiguration<TeacherReviewHistory>
{
    public void Configure(EntityTypeBuilder<TeacherReviewHistory> builder)
    {
        builder.ToTable("teacher_review_histories");

        builder.HasKey(h => h.HistoryId).HasName("pk_teacher_review_histories");

        builder.HasAlternateKey(h => new { h.CenterId, h.HistoryId })
            .HasName("ux_teacher_review_histories_center_id_history_id");

        builder.HasIndex(h => new { h.CenterId, h.AnalysisId })
            .HasDatabaseName("ix_teacher_review_histories_center_id_analysis_id");

        builder.HasIndex(h => new { h.CenterId, h.AttemptId })
            .HasDatabaseName("ix_teacher_review_histories_center_id_attempt_id");

        builder.HasIndex(h => new { h.CenterId, h.TeacherId })
            .HasDatabaseName("ix_teacher_review_histories_center_id_teacher_id");

        builder.Property(h => h.HistoryId).HasColumnName("history_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(h => h.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(h => h.AnalysisId).HasColumnName("analysis_id").HasColumnType("bigint unsigned");
        builder.Property(h => h.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned");
        builder.Property(h => h.TeacherId).HasColumnName("teacher_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(h => h.Decision).HasColumnName("decision").HasColumnType("varchar(32)").IsRequired();
        builder.Property(h => h.PreviousScore).HasColumnName("previous_score").HasColumnType("decimal(5,2)");
        builder.Property(h => h.NewScore).HasColumnName("new_score").HasColumnType("decimal(5,2)");
        builder.Property(h => h.PreviousIsCorrect).HasColumnName("previous_is_correct").HasColumnType("tinyint(1)");
        builder.Property(h => h.NewIsCorrect).HasColumnName("new_is_correct").HasColumnType("tinyint(1)");
        builder.Property(h => h.Note).HasColumnName("note").HasColumnType("varchar(1000)");
        builder.Property(h => h.OverrideVersion).HasColumnName("override_version").HasColumnType("int unsigned").HasDefaultValue(0u);

        builder.Property(h => h.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(h => h.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(h => h.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_teacher_review_histories_decision", "`decision` IN ('Approved', 'Adjusted')");
        });

        builder.HasOne(h => h.Analysis)
            .WithMany()
            .HasForeignKey(h => new { h.CenterId, h.AnalysisId })
            .HasPrincipalKey(a => new { a.CenterId, a.AnalysisId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_review_histories_reasoning_analyses");

        builder.HasOne(h => h.Attempt)
            .WithMany()
            .HasForeignKey(h => new { h.CenterId, h.AttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_review_histories_attempts");

        builder.HasOne(h => h.Teacher)
            .WithMany()
            .HasForeignKey(h => new { h.CenterId, h.TeacherId })
            .HasPrincipalKey(u => new { u.CenterId, u.UserId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_review_histories_teachers");
    }
}
