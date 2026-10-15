using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class LearningPathSessionRunConfiguration : IEntityTypeConfiguration<LearningPathSessionRun>
{
    public void Configure(EntityTypeBuilder<LearningPathSessionRun> builder)
    {
        builder.ToTable("learning_path_session_runs");

        builder.HasKey(r => r.SessionRunId).HasName("pk_learning_path_session_runs");

        builder.HasAlternateKey(r => new { r.CenterId, r.SessionRunId })
            .HasName("ux_learning_path_session_runs_center_id_session_run_id");

        builder.HasIndex(r => new { r.CenterId, r.StudentId, r.SessionId, r.ActiveKey })
            .IsUnique()
            .HasDatabaseName("ux_session_runs_center_student_session_active_key");

        builder.HasIndex(r => new { r.CenterId, r.SessionId, r.RunNumber })
            .HasDatabaseName("ix_session_runs_center_session_run_number");

        builder.HasIndex(r => new { r.CenterId, r.StudentId, r.Status })
            .HasDatabaseName("ix_session_runs_center_student_status");

        builder.Property(r => r.SessionRunId)
            .HasColumnName("session_run_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd();

        builder.Property(r => r.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(r => r.SessionId)
            .HasColumnName("session_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(r => r.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(r => r.RunNumber)
            .HasColumnName("run_number")
            .HasColumnType("int unsigned")
            .HasDefaultValue(1u);

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.EvaluationStatus)
            .HasColumnName("evaluation_status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.Outcome)
            .HasColumnName("outcome")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.ActiveKey)
            .HasColumnName("active_key")
            .HasColumnType("tinyint")
            .HasComputedColumnSql("CASE WHEN `status` IN ('InProgress', 'Grading') THEN 1 ELSE NULL END", stored: true);

        builder.Property(r => r.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(r => r.LastActivityAt)
            .HasColumnName("last_activity_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(r => r.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(r => r.CurrentQuestionId)
            .HasColumnName("current_question_id")
            .HasColumnType("bigint unsigned")
            .IsRequired(false);

        builder.Property(r => r.CheckpointQuestionId)
            .HasColumnName("checkpoint_question_id")
            .HasColumnType("bigint unsigned")
            .IsRequired(false);

        builder.Property(r => r.QuestionsAssigned)
            .HasColumnName("questions_assigned")
            .HasColumnType("json")
            .IsRequired(false)
            .HasConversion(
                v => v != null ? v.RootElement.ToString() : null,
                v => !string.IsNullOrEmpty(v) ? JsonDocument.Parse(v, new JsonDocumentOptions()) : null);

        builder.Property(r => r.PolicySnapshot)
            .HasColumnName("policy_snapshot")
            .HasColumnType("json")
            .IsRequired(false)
            .HasConversion(
                v => v != null ? v.RootElement.ToString() : null,
                v => !string.IsNullOrEmpty(v) ? JsonDocument.Parse(v, new JsonDocumentOptions()) : null);

        builder.Property(r => r.TotalQuestions)
            .HasColumnName("total_questions")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(r => r.QuestionsAttempted)
            .HasColumnName("questions_attempted")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(r => r.QuestionsCorrect)
            .HasColumnName("questions_correct")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(r => r.ScoreRate)
            .HasColumnName("score_rate")
            .HasColumnType("decimal(5,2)")
            .IsRequired(false);

        builder.Property(r => r.StartingMastery)
            .HasColumnName("starting_mastery")
            .HasColumnType("decimal(5,2)")
            .IsRequired(false);

        builder.Property(r => r.EndingMastery)
            .HasColumnName("ending_mastery")
            .HasColumnType("decimal(5,2)")
            .IsRequired(false);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(r => r.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_learning_path_session_runs_status", "`status` IN ('InProgress', 'Grading', 'NeedsReview', 'Completed', 'Abandoned')");
            t.HasCheckConstraint("ck_learning_path_session_runs_evaluation_status", "`evaluation_status` IN ('NotStarted', 'Queued', 'Processing', 'AwaitingTeacher', 'Completed', 'Failed')");
            t.HasCheckConstraint("ck_learning_path_session_runs_outcome", "`outcome` IN ('None', 'Passed', 'NotPassed', 'InsufficientEvidence')");
        });

        builder.HasOne(r => r.Session)
            .WithMany(s => s.Runs)
            .HasForeignKey(r => new { r.CenterId, r.SessionId })
            .HasPrincipalKey(s => new { s.CenterId, s.SessionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_session_runs_sessions_session");

        builder.HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_session_runs_students_student");

        builder.HasOne(r => r.CurrentQuestion)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.CurrentQuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_session_runs_questions_current_question");

        builder.HasOne(r => r.CheckpointQuestion)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.CheckpointQuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_session_runs_questions_checkpoint_question");
    }
}
