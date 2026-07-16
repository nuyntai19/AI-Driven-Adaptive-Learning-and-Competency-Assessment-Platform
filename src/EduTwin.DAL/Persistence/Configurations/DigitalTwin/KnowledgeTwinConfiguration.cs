using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.DAL.Persistence.Configurations.DigitalTwin;

public class KnowledgeTwinConfiguration : IEntityTypeConfiguration<KnowledgeTwin>
{
    public void Configure(EntityTypeBuilder<KnowledgeTwin> builder)
    {
        builder.ToTable("knowledge_twins");

        builder.HasKey(t => t.KnowledgeTwinId).HasName("pk_knowledge_twins");

        builder.HasIndex(t => new { t.CenterId, t.StudentId, t.TopicNodeId })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_twins_center_id_student_id_topic_node_id");

        builder.HasIndex(t => new { t.CenterId, t.SubjectId, t.MasteryPercentage })
            .HasDatabaseName("ix_knowledge_twins_center_id_subject_id_mastery_percentage");

        builder.HasIndex(t => new { t.CenterId, t.TopicNodeId })
            .HasDatabaseName("ix_knowledge_twins_center_id_topic_node_id");

        builder.Property(t => t.KnowledgeTwinId).HasColumnName("knowledge_twin_id").HasColumnType("bigint unsigned").ValueGeneratedNever();
        builder.Property(t => t.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(t => t.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(t => t.TopicNodeId).HasColumnName("topic_node_id").HasColumnType("bigint unsigned");
        builder.Property(t => t.MasteryPercentage).HasColumnName("mastery_percentage").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(t => t.EvidenceCount).HasColumnName("evidence_count").HasColumnType("int unsigned").HasDefaultValue(0u);
        builder.Property(t => t.LastReasoningQuality).HasColumnName("last_reasoning_quality").HasColumnType("decimal(5,2)");
        builder.Property(t => t.LastAttemptId).HasColumnName("last_attempt_id").HasColumnType("bigint unsigned");
        builder.Property(t => t.LastEvidenceAt).HasColumnName("last_evidence_at").HasColumnType("datetime(6)");

        builder.Property(t => t.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(t => t.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_knowledge_twins_mastery_percentage", "`mastery_percentage` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_knowledge_twins_last_reasoning_quality", "`last_reasoning_quality` IS NULL OR `last_reasoning_quality` BETWEEN 0 AND 100");
        });

        builder.HasOne(t => t.Student)
            .WithMany()
            .HasForeignKey(t => new { t.CenterId, t.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_twins_students_student");

        builder.HasOne(t => t.Subject)
            .WithMany()
            .HasForeignKey(t => new { t.CenterId, t.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_twins_subjects_subject");

        builder.HasOne(t => t.TopicNode)
            .WithMany()
            .HasForeignKey(t => new { t.CenterId, t.TopicNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_twins_knowledge_nodes_topic_node");
    }
}
