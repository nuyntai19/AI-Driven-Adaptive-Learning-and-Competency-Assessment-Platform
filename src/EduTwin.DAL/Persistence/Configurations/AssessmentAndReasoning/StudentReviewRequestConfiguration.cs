using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public class StudentReviewRequestConfiguration : IEntityTypeConfiguration<StudentReviewRequest>
{
    public void Configure(EntityTypeBuilder<StudentReviewRequest> builder)
    {
        builder.ToTable("student_review_requests");

        builder.HasKey(r => r.RequestId).HasName("pk_student_review_requests");

        builder.HasAlternateKey(r => new { r.CenterId, r.RequestId })
            .HasName("ux_student_review_requests_center_id_request_id");

        builder.HasIndex(r => new { r.CenterId, r.AttemptId })
            .HasDatabaseName("ix_student_review_requests_center_id_attempt_id");

        builder.HasIndex(r => new { r.CenterId, r.StudentId, r.Status })
            .HasDatabaseName("ix_student_review_requests_center_id_student_id_status");

        builder.Property(r => r.RequestId)
            .HasColumnName("request_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(r => r.AttemptId)
            .HasColumnName("attempt_id")
            .HasColumnType("bigint unsigned")
            .IsRequired();

        builder.Property(r => r.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(r => r.QuestionId)
            .HasColumnName("question_id")
            .HasColumnType("bigint unsigned")
            .IsRequired();

        builder.Property(r => r.StudentComment)
            .HasColumnName("student_comment")
            .HasColumnType("varchar(1000)")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.TeacherNote)
            .HasColumnName("teacher_note")
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(r => r.ResolvedByTeacherId)
            .HasColumnName("resolved_by_teacher_id")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(r => r.ResolvedAt)
            .HasColumnName("resolved_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(r => r.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(r => r.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .HasDefaultValue(1ul)
            .IsConcurrencyToken();

        builder.HasOne(r => r.Attempt)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.AttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AttemptId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_student_review_requests_attempts");

        builder.HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_review_requests_students");

        builder.HasOne(r => r.Question)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.QuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_review_requests_questions");

        builder.HasOne(r => r.ResolvedByTeacher)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.ResolvedByTeacherId })
            .HasPrincipalKey(t => new { t.CenterId, t.TeacherId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_review_requests_teachers");
    }
}
