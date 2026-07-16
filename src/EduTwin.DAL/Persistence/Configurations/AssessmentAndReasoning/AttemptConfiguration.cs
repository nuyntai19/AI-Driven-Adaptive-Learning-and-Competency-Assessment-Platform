using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("attempts");

        builder.HasKey(a => a.AttemptId).HasName("pk_attempts");

        builder.HasAlternateKey(a => new { a.CenterId, a.AttemptId })
            .HasName("ux_attempts_center_id_attempt_id");

        builder.HasIndex(a => new { a.CenterId, a.StudentId, a.ClientSubmissionId })
            .IsUnique()
            .HasDatabaseName("ux_attempts_center_id_student_id_client_submission_id");

        builder.HasIndex(a => new { a.CenterId, a.StudentId, a.QuestionId, a.CreatedAt })
            .HasDatabaseName("ix_attempts_center_id_student_id_question_id_created_at");

        builder.HasIndex(a => new { a.CenterId, a.AssignmentId, a.StudentId })
            .HasDatabaseName("ix_attempts_center_id_assignment_id_student_id");

        builder.HasIndex(a => new { a.CenterId, a.Status, a.CreatedAt })
            .HasDatabaseName("ix_attempts_center_id_status_created_at");

        builder.HasIndex(a => new { a.CenterId, a.QuestionId })
            .HasDatabaseName("ix_attempts_center_id_question_id");

        builder.Property(a => a.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(a => a.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(a => a.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(a => a.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned");
        builder.Property(a => a.AssignmentId).HasColumnName("assignment_id").HasColumnType("varchar(36)");
        builder.Property(a => a.FinalAnswer).HasColumnName("final_answer").HasColumnType("longtext").IsRequired();
        builder.Property(a => a.ReasoningText).HasColumnName("reasoning_text").HasColumnType("longtext");
        builder.Property(a => a.IsCorrect).HasColumnName("is_correct").HasColumnType("tinyint(1)");
        builder.Property(a => a.AwardedScore).HasColumnName("awarded_score").HasColumnType("decimal(5,2)");
        builder.Property(a => a.TimeSpentSeconds).HasColumnName("time_spent_seconds").HasColumnType("int unsigned");
        builder.Property(a => a.Confidence).HasColumnName("confidence").HasColumnType("decimal(5,2)");
        builder.Property(a => a.AnswerChanges).HasColumnName("answer_changes").HasColumnType("int unsigned").HasDefaultValue(0);
        builder.Property(a => a.Skipped).HasColumnName("skipped").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(a => a.ReasoningLanguage).HasColumnName("reasoning_language").HasColumnType("varchar(8)").IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.ClientSubmissionId).HasColumnName("client_submission_id").HasColumnType("varchar(36)").IsRequired();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(a => a.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_attempts_confidence", "`confidence` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_attempts_time_spent_seconds", "`time_spent_seconds` >= 0");
            t.HasCheckConstraint("ck_attempts_reasoning_language", "`reasoning_language` IN ('vi', 'en')");
            t.HasCheckConstraint("ck_attempts_status", "`status` IN ('PendingAnalysis', 'Processing', 'Completed', 'NeedsTeacherReview')");
        });

        builder.HasOne(a => a.Student)
            .WithMany()
            .HasForeignKey(a => new { a.CenterId, a.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attempts_students_student");

        builder.HasOne(a => a.Question)
            .WithMany()
            .HasForeignKey(a => new { a.CenterId, a.QuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attempts_questions_question");

        builder.HasOne(a => a.Assignment)
            .WithMany()
            .HasForeignKey(a => new { a.CenterId, a.AssignmentId })
            .HasPrincipalKey(assign => new { assign.CenterId, assign.AssignmentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attempts_assignments_assignment");
    }
}
