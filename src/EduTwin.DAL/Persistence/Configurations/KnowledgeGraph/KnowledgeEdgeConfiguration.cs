using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.DAL.Persistence.Configurations.KnowledgeGraph;

public class KnowledgeEdgeConfiguration : IEntityTypeConfiguration<KnowledgeEdge>
{
    public void Configure(EntityTypeBuilder<KnowledgeEdge> builder)
    {
        builder.ToTable("knowledge_edges");

        // Primary Key
        builder.HasKey(e => e.EdgeId).HasName("pk_knowledge_edges");

        // Properties
        builder.Property(e => e.EdgeId)
            .HasColumnName("edge_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(e => e.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(e => e.SourceNodeId)
            .HasColumnName("source_node_id")
            .HasColumnType("bigint unsigned");

        builder.Property(e => e.TargetNodeId)
            .HasColumnName("target_node_id")
            .HasColumnType("bigint unsigned");

        builder.Property(e => e.RelationType)
            .HasColumnName("relation_type")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.Weight)
            .HasColumnName("weight")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(1.00m);

        // MTA Properties
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(e => e.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(e => e.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(e => e.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(e => new { e.CenterId, e.EdgeId })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_edges_center_id_edge_id");

        builder.HasIndex(e => new { e.CenterId, e.SourceNodeId, e.TargetNodeId, e.RelationType })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_edges_center_id_source_id_target_id_relation_type");

        builder.HasIndex(e => new { e.CenterId, e.TargetNodeId, e.RelationType })
            .HasDatabaseName("ix_knowledge_edges_center_id_target_node_id_relation_type");

        // Explicit index for self FK source and target
        builder.HasIndex(e => new { e.CenterId, e.SourceNodeId })
            .HasDatabaseName("ix_knowledge_edges_center_id_source_node_id");

        // Explicit index for subject composite FK (child side)
        builder.HasIndex(e => new { e.CenterId, e.SubjectId })
            .HasDatabaseName("ix_knowledge_edges_center_id_subject_id");

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_knowledge_edges_self_loop", "source_node_id <> target_node_id");
            t.HasCheckConstraint("ck_knowledge_edges_weight", "weight BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_knowledge_edges_relation_type", "relation_type IN ('PrerequisiteOf', 'RelatedTo', 'PartOf', 'CausesErrorIn')");
        });

        // Relations
        builder.HasOne(e => e.Subject)
            .WithMany()
            .HasForeignKey(e => new { e.CenterId, e.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_edges_subjects_subject");

        builder.HasOne(e => e.SourceNode)
            .WithMany()
            .HasForeignKey(e => new { e.CenterId, e.SourceNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_edges_knowledge_nodes_source");

        builder.HasOne(e => e.TargetNode)
            .WithMany()
            .HasForeignKey(e => new { e.CenterId, e.TargetNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_knowledge_edges_knowledge_nodes_target");
    }
}
