using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.DAL.Persistence.Configurations.KnowledgeGraph;

public class KnowledgeNodeConfiguration : IEntityTypeConfiguration<KnowledgeNode>
{
    public void Configure(EntityTypeBuilder<KnowledgeNode> builder)
    {
        builder.ToTable("knowledge_nodes");

        // Primary Key
        builder.HasKey(n => n.NodeId).HasName("pk_knowledge_nodes");

        // Alternate Key
        builder.HasAlternateKey(n => new { n.CenterId, n.NodeId })
            .HasName("ux_knowledge_nodes_center_id_node_id");

        // Properties
        builder.Property(n => n.NodeId)
            .HasColumnName("node_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd();

        builder.Property(n => n.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(n => n.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(n => n.ParentNodeId)
            .HasColumnName("parent_node_id")
            .HasColumnType("bigint unsigned");

        builder.Property(n => n.NodeType)
            .HasColumnName("node_type")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(n => n.NodeCode)
            .HasColumnName("node_code")
            .HasColumnType("varchar(64)")
            .IsRequired();

        builder.Property(n => n.NodeName)
            .HasColumnName("node_name")
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(n => n.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(n => n.OrderIndex)
            .HasColumnName("order_index")
            .HasColumnType("int unsigned")
            .HasDefaultValue(0u);

        builder.Property(n => n.ExamImportance)
            .HasColumnName("exam_importance")
            .HasColumnType("decimal(5,2)");

        builder.Property(n => n.EstimatedLearningMinutes)
            .HasColumnName("estimated_learning_minutes")
            .HasColumnType("int unsigned");

        builder.Property(n => n.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(true);

        // MTA Properties
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(n => n.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(n => n.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(n => n.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(n => n.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(n => n.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(n => new { n.CenterId, n.SubjectId, n.NodeCode })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_nodes_center_id_subject_id_node_code");

        builder.HasIndex(n => new { n.CenterId, n.SubjectId, n.NodeType, n.OrderIndex })
            .HasDatabaseName("ix_knowledge_nodes_center_id_subject_id_node_type_order_index");

        // Explicit index for self FK
        builder.HasIndex(n => new { n.CenterId, n.ParentNodeId })
            .HasDatabaseName("ix_knowledge_nodes_center_id_parent_node_id");

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_knowledge_nodes_node_type", "node_type IN ('Subject', 'Chapter', 'Topic', 'Skill', 'Concept')");
            t.HasCheckConstraint("ck_knowledge_nodes_exam_importance", "exam_importance BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_knowledge_nodes_estimated_learning_minutes", "estimated_learning_minutes > 0");
        });

        // Relations
        builder.HasOne(n => n.Subject)
            .WithMany()
            .HasForeignKey(n => new { n.CenterId, n.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_nodes_subjects_subject");

        builder.HasOne(n => n.ParentNode)
            .WithMany()
            .HasForeignKey(n => new { n.CenterId, n.ParentNodeId })
            .HasPrincipalKey(p => new { p.CenterId, p.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_nodes_knowledge_nodes_parent");
    }
}
