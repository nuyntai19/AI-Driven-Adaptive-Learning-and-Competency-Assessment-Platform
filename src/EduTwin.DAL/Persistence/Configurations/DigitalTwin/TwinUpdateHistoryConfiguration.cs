using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.DAL.Persistence.Configurations.DigitalTwin;

public class TwinUpdateHistoryConfiguration : IEntityTypeConfiguration<TwinUpdateHistory>
{
    public void Configure(EntityTypeBuilder<TwinUpdateHistory> builder)
    {
        builder.ToTable("twin_update_history");

        builder.HasKey(h => h.HistoryId).HasName("pk_twin_update_history");

        builder.HasIndex(h => new { h.CenterId, h.StudentId, h.SubjectId, h.CreatedAt })
            .HasDatabaseName("ix_twin_update_history_center_student_subject_created_at");

        builder.HasIndex(h => new { h.CenterId, h.TopicNodeId, h.CreatedAt })
            .HasDatabaseName("ix_twin_update_history_center_id_topic_node_id_created_at");

        builder.HasIndex(h => new { h.CenterId, h.AttemptId })
            .HasDatabaseName("ix_twin_update_history_center_id_attempt_id");

        builder.HasIndex(h => new { h.CenterId, h.SubjectId })
            .HasDatabaseName("ix_twin_update_history_center_id_subject_id");

        builder.Property(h => h.HistoryId).HasColumnName("history_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(h => h.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(h => h.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(h => h.TopicNodeId).HasColumnName("topic_node_id").HasColumnType("bigint unsigned");
        builder.Property(h => h.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned");
        builder.Property(h => h.AnalysisId).HasColumnName("analysis_id").HasColumnType("bigint unsigned");

        builder.Property(h => h.EventSource)
            .HasColumnName("event_source")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(h => h.PreviousMastery).HasColumnName("previous_mastery").HasColumnType("decimal(5,2)");
        builder.Property(h => h.NewMastery).HasColumnName("new_mastery").HasColumnType("decimal(5,2)");
        builder.Property(h => h.MasteryDelta).HasColumnName("mastery_delta").HasColumnType("decimal(6,2)");
        builder.Property(h => h.EffectiveReasoningQuality).HasColumnName("effective_reasoning_quality").HasColumnType("decimal(5,2)");
        builder.Property(h => h.CalculationVersion).HasColumnName("calculation_version").HasColumnType("varchar(20)").IsRequired();
        builder.Property(h => h.CalculationBreakdown)
            .HasColumnName("calculation_breakdown")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(
                v => v.RootElement.ToString(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));
        builder.Property(h => h.Explanation).HasColumnName("explanation").HasColumnType("varchar(1000)").IsRequired();

        builder.Property(h => h.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(h => h.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_twin_update_history_event_source", "`event_source` IN ('AIAnalysis', 'RuleFallback', 'TeacherOverride', 'Replay')");
            t.HasCheckConstraint("ck_twin_update_history_previous_mastery", "`previous_mastery` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_twin_update_history_new_mastery", "`new_mastery` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_twin_update_history_effective_reasoning_quality", "`effective_reasoning_quality` IS NULL OR `effective_reasoning_quality` BETWEEN 0 AND 100");
        });

        builder.HasOne(h => h.Student)
            .WithMany()
            .HasForeignKey(h => new { h.CenterId, h.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_twin_update_history_students_student");

        builder.HasOne(h => h.Subject)
            .WithMany()
            .HasForeignKey(h => new { h.CenterId, h.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_twin_update_history_subjects_subject");

        builder.HasOne(h => h.TopicNode)
            .WithMany()
            .HasForeignKey(h => new { h.CenterId, h.TopicNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_twin_update_history_knowledge_nodes_topic_node");
    }
}
