using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.Assignments;

namespace EduTwin.DAL.Persistence.Configurations.Assignments;

public class StudentAssignmentProgressConfiguration : IEntityTypeConfiguration<StudentAssignmentProgress>
{
    public void Configure(EntityTypeBuilder<StudentAssignmentProgress> builder)
    {
        builder.ToTable("student_assignment_progress");

        // Primary Key
        builder.HasKey(p => p.ProgressId).HasName("pk_student_assignment_progress");

        // Unique Index
        builder.HasIndex(p => new { p.CenterId, p.AssignmentId, p.StudentId })
            .IsUnique()
            .HasDatabaseName("ux_student_assignment_progress_center_assignment_id_student_id");

        // Properties
        builder.Property(p => p.ProgressId)
            .HasColumnName("progress_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedNever();

        builder.Property(p => p.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.AssignmentId)
            .HasColumnName("assignment_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.CompletedQuestionCount)
            .HasColumnName("completed_question_count")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(p => p.TotalQuestionCount)
            .HasColumnName("total_question_count")
            .HasColumnType("int unsigned");

        builder.Property(p => p.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("datetime(6)");

        builder.Property(p => p.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("datetime(6)");

        builder.Property(p => p.TeacherFinalReviewStatus)
            .HasColumnName("teacher_final_review_status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .HasDefaultValue(TeacherFinalReviewStatus.Pending)
            .IsRequired();

        builder.Property(p => p.FinalReviewedByUserId)
            .HasColumnName("final_reviewed_by_user_id")
            .HasColumnType("varchar(36)");

        builder.Property(p => p.FinalReviewedAt)
            .HasColumnName("final_reviewed_at")
            .HasColumnType("datetime(6)");

        builder.Property(p => p.FinalTeacherNote)
            .HasColumnName("final_teacher_note")
            .HasColumnType("varchar(1000)");

        builder.Property(p => p.FinalReviewVersion)
            .HasColumnName("final_review_version")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(p => p.OverallAiComment)
            .HasColumnName("overall_ai_comment")
            .HasColumnType("longtext");

        builder.Property(p => p.OverallAiCommentGeneratedAt)
            .HasColumnName("overall_ai_comment_generated_at")
            .HasColumnType("datetime(6)");

        builder.Property(p => p.OverallAiCommentVersion)
            .HasColumnName("overall_ai_comment_version")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(p => p.IsOverallAiCommentStale)
            .HasColumnName("is_overall_ai_comment_stale")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false);

        // MTA Properties
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(p => p.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(p => new { p.CenterId, p.StudentId, p.Status })
            .HasDatabaseName("ix_student_assignment_progress_center_id_student_id_status");

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_student_assignment_progress_counts", "completed_question_count <= total_question_count");
            t.HasCheckConstraint("ck_student_assignment_progress_status", "status IN ('NotStarted', 'InProgress', 'Completed', 'Overdue')");
            t.HasCheckConstraint("ck_student_assignment_progress_final_review", "teacher_final_review_status IN ('Pending', 'Approved')");
        });

        // Relations
        builder.HasOne(p => p.Assignment)
            .WithMany()
            .HasForeignKey(p => new { p.CenterId, p.AssignmentId })
            .HasPrincipalKey(a => new { a.CenterId, a.AssignmentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_assignment_progress_assignments_assignment");

        builder.HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => new { p.CenterId, p.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_assignment_progress_students_student");
    }
}
