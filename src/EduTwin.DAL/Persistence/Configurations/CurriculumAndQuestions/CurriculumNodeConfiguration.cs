using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public class CurriculumNodeConfiguration : IEntityTypeConfiguration<CurriculumNode>
{
    public void Configure(EntityTypeBuilder<CurriculumNode> builder)
    {
        builder.ToTable("curriculum_nodes");

        // Primary Key
        builder.HasKey(cn => new { cn.CenterId, cn.CurriculumId, cn.NodeId }).HasName("pk_curriculum_nodes");

        // Properties
        builder.Property(cn => cn.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)");

        builder.Property(cn => cn.CurriculumId)
            .HasColumnName("curriculum_id")
            .HasColumnType("varchar(36)");

        builder.Property(cn => cn.NodeId)
            .HasColumnName("node_id")
            .HasColumnType("bigint unsigned");

        builder.Property(cn => cn.OrderIndex)
            .HasColumnName("order_index")
            .HasColumnType("int unsigned");

        builder.Property(cn => cn.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        // Indexes
        builder.HasIndex(cn => new { cn.CenterId, cn.CurriculumId, cn.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_curriculum_nodes_center_id_curriculum_id_order_index");

        builder.HasIndex(cn => new { cn.CenterId, cn.NodeId })
            .HasDatabaseName("ix_curriculum_nodes_center_id_node_id");

        // Relations
        builder.HasOne(cn => cn.Curriculum)
            .WithMany()
            .HasForeignKey(cn => new { cn.CenterId, cn.CurriculumId })
            .HasPrincipalKey(c => new { c.CenterId, c.CurriculumId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curriculum_nodes_curriculums_curriculum");

        builder.HasOne(cn => cn.Node)
            .WithMany()
            .HasForeignKey(cn => new { cn.CenterId, cn.NodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curriculum_nodes_knowledge_nodes_node");
    }
}
