using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AdaptivePractice;

namespace EduTwin.DAL.Persistence.Configurations.AdaptivePractice;

public class AdaptivePracticeSessionSummaryConfiguration : IEntityTypeConfiguration<AdaptivePracticeSessionSummary>
{
    public void Configure(EntityTypeBuilder<AdaptivePracticeSessionSummary> builder)
    {
        builder.ToTable("adaptive_practice_session_summaries");

        builder.HasKey(s => s.SessionId).HasName("pk_adaptive_practice_session_summaries");

        builder.HasAlternateKey(s => new { s.CenterId, s.SessionId })
            .HasName("ux_adaptive_practice_summaries_center_id_session_id");

        builder.HasIndex(s => new { s.CenterId, s.StudentId })
            .HasDatabaseName("ix_adaptive_practice_summaries_center_student");

        builder.Property(s => s.SessionId).HasColumnName("session_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(s => s.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(s => s.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();

        builder.Property(s => s.TotalQuestions).HasColumnName("total_questions").HasColumnType("int").IsRequired();
        builder.Property(s => s.CorrectCount).HasColumnName("correct_count").HasColumnType("int").IsRequired();
        builder.Property(s => s.IncorrectCount).HasColumnName("incorrect_count").HasColumnType("int").IsRequired();
        builder.Property(s => s.PartialCount).HasColumnName("partial_count").HasColumnType("int").IsRequired();
        builder.Property(s => s.PendingCount).HasColumnName("pending_count").HasColumnType("int").IsRequired();
        builder.Property(s => s.TimeSpentSeconds).HasColumnName("time_spent_seconds").HasColumnType("int").IsRequired();

        builder.Property(s => s.StartingMastery).HasColumnName("starting_mastery").HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(s => s.EndingMastery).HasColumnName("ending_mastery").HasColumnType("decimal(5,2)").IsRequired(false);
        builder.Property(s => s.GoalAchievementStatus).HasColumnName("goal_achievement_status").HasColumnType("varchar(64)").IsRequired();

        builder.Property(s => s.StrongSkillIds).HasColumnName("strong_skill_ids").HasColumnType("text").IsRequired(false);
        builder.Property(s => s.WeakSkillIds).HasColumnName("weak_skill_ids").HasColumnType("text").IsRequired(false);
        builder.Property(s => s.RemediationSkillIds).HasColumnName("remediation_skill_ids").HasColumnType("text").IsRequired(false);

        builder.Property(s => s.SummaryConclusion).HasColumnName("summary_conclusion").HasColumnType("text").IsRequired();
        builder.Property(s => s.RecommendedNextAction).HasColumnName("recommended_next_action").HasColumnType("text").IsRequired();
        builder.Property(s => s.RecommendedNextTopicId).HasColumnName("recommended_next_topic_id").HasColumnType("bigint unsigned").IsRequired(false);
        builder.Property(s => s.SummaryStatus).HasColumnName("summary_status").HasColumnType("varchar(32)").HasDefaultValue("Finalized");

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

        builder.HasOne(s => s.RecommendedNextTopic)
            .WithMany()
            .HasForeignKey(s => new { s.CenterId, s.RecommendedNextTopicId })
            .HasPrincipalKey(kn => new { kn.CenterId, kn.NodeId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
