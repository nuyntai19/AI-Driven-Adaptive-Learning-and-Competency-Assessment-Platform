using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class LearningPathSessionConfiguration : IEntityTypeConfiguration<LearningPathSession>
{
    public void Configure(EntityTypeBuilder<LearningPathSession> builder)
    {
        builder.ToTable("learning_path_sessions");
        builder.Property(s => s.PlannedDate)
            .HasColumnName("planned_date")
            .HasColumnType("date")
            .HasConversion(
                d => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                d => d.HasValue ? DateOnly.FromDateTime(d.Value) : (DateOnly?)null);

        builder.HasKey(s => s.SessionId).HasName("pk_learning_path_sessions");

        builder.HasAlternateKey(s => new { s.CenterId, s.SessionId })
            .HasName("ux_learning_path_sessions_center_id_session_id");

        builder.HasIndex(s => new { s.CenterId, s.LearningPathId, s.SessionOrder })
            .HasDatabaseName("ix_lp_sessions_center_path_session_order");

        builder.HasIndex(s => new { s.CenterId, s.TopicNodeId })
            .HasDatabaseName("ix_learning_path_sessions_center_id_topic_node_id");

        builder.HasIndex(s => new { s.CenterId, s.LegacySessionId })
            .IsUnique()
            .HasDatabaseName("ux_learning_path_sessions_center_id_legacy_session_id");

        builder.Property(s => s.SessionId)
            .HasColumnName("session_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(s => s.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(s => s.LearningPathId)
            .HasColumnName("learning_path_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(s => s.LegacySessionId)
            .HasColumnName("legacy_session_id")
            .HasColumnType("varchar(128)")
            .IsRequired(false);

        builder.Property(s => s.TopicNodeId)
            .HasColumnName("topic_node_id")
            .HasColumnType("bigint unsigned")
            .IsRequired();

        builder.Property(s => s.ClassId)
            .HasColumnName("class_id")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(s => s.PhaseNumber)
            .HasColumnName("phase_number")
            .HasColumnType("int unsigned")
            .IsRequired();

        builder.Property(s => s.WeekNumber)
            .HasColumnName("week_number")
            .HasColumnType("int unsigned")
            .IsRequired();

        builder.Property(s => s.SessionOrder)
            .HasColumnName("session_order")
            .HasColumnType("int unsigned")
            .IsRequired();

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(s => s.Objective)
            .HasColumnName("objective")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(s => s.Rationale)
            .HasColumnName("rationale")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(s => s.PracticeQuestionIds)
            .HasColumnName("practice_question_ids")
            .HasColumnType("json")
            .IsRequired(false)
            .HasConversion(
                v => v != null ? v.RootElement.ToString() : null,
                v => !string.IsNullOrEmpty(v) ? JsonDocument.Parse(v, new JsonDocumentOptions()) : null);

        builder.Property(s => s.CheckpointQuestionIds)
            .HasColumnName("checkpoint_question_ids")
            .HasColumnType("json")
            .IsRequired(false)
            .HasConversion(
                v => v != null ? v.RootElement.ToString() : null,
                v => !string.IsNullOrEmpty(v) ? JsonDocument.Parse(v, new JsonDocumentOptions()) : null);

        builder.Property(s => s.MinimumQuestions)
            .HasColumnName("minimum_questions")
            .HasColumnType("int unsigned")
            .HasDefaultValue(1u);

        builder.Property(s => s.RequiredCorrectRate)
            .HasColumnName("required_correct_rate")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0.70m);

        builder.Property(s => s.PolicyVersion)
            .HasColumnName("policy_version")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("session-policy-v1");

        builder.Property(s => s.CurrentRunId)
            .HasColumnName("current_run_id")
            .HasColumnType("bigint unsigned")
            .IsRequired(false);

        builder.Property(s => s.TotalRuns)
            .HasColumnName("total_runs")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(s => s.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_learning_path_sessions_status", "`status` IN ('NotStarted', 'InProgress', 'Completed', 'NeedsReview', 'Skipped')");
            t.HasCheckConstraint("ck_learning_path_sessions_required_correct_rate", "`required_correct_rate` BETWEEN 0 AND 1");
        });

        builder.HasOne(s => s.LearningPath)
            .WithMany(l => l.Sessions)
            .HasForeignKey(s => new { s.CenterId, s.LearningPathId })
            .HasPrincipalKey(l => new { l.CenterId, l.LearningPathId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_sessions_learning_paths_learning_path");

        builder.HasOne(s => s.TopicNode)
            .WithMany()
            .HasForeignKey(s => new { s.CenterId, s.TopicNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_sessions_knowledge_nodes_topic_node");

        builder.HasOne(s => s.Class)
            .WithMany()
            .HasForeignKey(s => new { s.CenterId, s.ClassId })
            .HasPrincipalKey(c => new { c.CenterId, c.ClassId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_sessions_classes_class");
    }
}
