using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AdaptivePractice;

namespace EduTwin.DAL.Persistence.Configurations.AdaptivePractice;

public class AdaptivePracticeSessionConfiguration : IEntityTypeConfiguration<AdaptivePracticeSession>
{
    public void Configure(EntityTypeBuilder<AdaptivePracticeSession> builder)
    {
        builder.ToTable("adaptive_practice_sessions");

        builder.HasKey(s => s.SessionId).HasName("pk_adaptive_practice_sessions");

        builder.HasAlternateKey(s => new { s.CenterId, s.SessionId })
            .HasName("ux_adaptive_practice_sessions_center_id_session_id");

        builder.HasIndex(s => new { s.CenterId, s.StudentId, s.Status })
            .HasDatabaseName("ix_adaptive_practice_sessions_center_student_status");

        builder.HasIndex(s => new { s.CenterId, s.StudentId, s.SubjectId })
            .HasDatabaseName("ix_adaptive_practice_sessions_center_student_subject");

        builder.Property(s => s.SessionId).HasColumnName("session_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(s => s.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(s => s.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(s => s.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(s => s.TopicNodeId).HasColumnName("topic_node_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(s => s.LearningPathId).HasColumnName("learning_path_id").HasColumnType("varchar(36)").IsRequired(false);
        builder.Property(s => s.LearningPathItemId).HasColumnName("learning_path_item_id").HasColumnType("bigint unsigned").IsRequired(false);

        builder.Property(s => s.PracticeMode)
            .HasColumnName("practice_mode")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.StartingMastery).HasColumnName("starting_mastery").HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(s => s.TargetMastery).HasColumnName("target_mastery").HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(s => s.CurrentMastery).HasColumnName("current_mastery").HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(s => s.TargetSkillIds).HasColumnName("target_skill_ids").HasColumnType("text").IsRequired(false);
        builder.Property(s => s.TimeBudgetMinutes).HasColumnName("time_budget_minutes").HasColumnType("int").IsRequired();
        builder.Property(s => s.MinimumQuestions).HasColumnName("minimum_questions").HasColumnType("int").IsRequired();
        builder.Property(s => s.MaximumQuestions).HasColumnName("maximum_questions").HasColumnType("int").IsRequired();
        builder.Property(s => s.QuestionCount).HasColumnName("question_count").HasColumnType("int").HasDefaultValue(0);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.CloseReason)
            .HasColumnName("close_reason")
            .HasColumnType("varchar(64)")
            .HasConversion<string>()
            .IsRequired(false);

        builder.Property(s => s.SelectionReason).HasColumnName("selection_reason").HasColumnType("text").IsRequired();
        builder.Property(s => s.PolicyVersion).HasColumnName("policy_version").HasColumnType("varchar(16)").HasDefaultValue("1.0");

        builder.Property(s => s.CurrentDifficulty).HasColumnName("current_difficulty").HasColumnType("tinyint unsigned").HasDefaultValue((byte)2);
        builder.Property(s => s.ConsecutiveCorrect).HasColumnName("consecutive_correct").HasColumnType("int").HasDefaultValue(0);
        builder.Property(s => s.ConsecutiveIncorrect).HasColumnName("consecutive_incorrect").HasColumnType("int").HasDefaultValue(0);
        builder.Property(s => s.IsDiagnosticMode).HasColumnName("is_diagnostic_mode").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(s => s.DiagnosticQuestionsCount).HasColumnName("diagnostic_questions_count").HasColumnType("int").HasDefaultValue(0);
        builder.Property(s => s.PrerequisiteTestedNodeId).HasColumnName("prerequisite_tested_node_id").HasColumnType("bigint unsigned").IsRequired(false);

        builder.Property(s => s.StartedAt).HasColumnName("started_at").HasColumnType("datetime(6)").IsRequired(false);
        builder.Property(s => s.PausedAt).HasColumnName("paused_at").HasColumnType("datetime(6)").IsRequired(false);
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at").HasColumnType("datetime(6)").IsRequired(false);

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)").IsRequired(false);
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)").IsRequired(false);
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)").IsRequired(false);
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)").IsRequired(false);
        builder.Property(s => s.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").IsConcurrencyToken();

        // Foreign keys
        builder.HasOne(s => s.Student)
            .WithMany()
            .HasForeignKey(s => new { s.CenterId, s.StudentId })
            .HasPrincipalKey(st => new { st.CenterId, st.StudentId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Subject)
            .WithMany()
            .HasForeignKey(s => new { s.CenterId, s.SubjectId })
            .HasPrincipalKey(sb => new { sb.CenterId, sb.SubjectId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.TopicNode)
            .WithMany()
            .HasForeignKey(s => new { s.CenterId, s.TopicNodeId })
            .HasPrincipalKey(kn => new { kn.CenterId, kn.NodeId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Summary)
            .WithOne(sum => sum.Session)
            .HasForeignKey<AdaptivePracticeSessionSummary>(sum => new { sum.CenterId, sum.SessionId })
            .HasPrincipalKey<AdaptivePracticeSession>(s => new { s.CenterId, s.SessionId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
