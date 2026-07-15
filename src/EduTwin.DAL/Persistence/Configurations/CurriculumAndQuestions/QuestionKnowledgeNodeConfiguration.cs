using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public class QuestionKnowledgeNodeConfiguration : IEntityTypeConfiguration<QuestionKnowledgeNode>
{
    public void Configure(EntityTypeBuilder<QuestionKnowledgeNode> builder)
    {
        builder.ToTable("question_knowledge_nodes");

        // Primary Key
        builder.HasKey(qkn => new { qkn.CenterId, qkn.QuestionId, qkn.NodeId, qkn.MappingRole })
            .HasName("pk_question_knowledge_nodes");

        // Properties
        builder.Property(qkn => qkn.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)");

        builder.Property(qkn => qkn.QuestionId)
            .HasColumnName("question_id")
            .HasColumnType("bigint unsigned");

        builder.Property(qkn => qkn.NodeId)
            .HasColumnName("node_id")
            .HasColumnType("bigint unsigned");

        builder.Property(qkn => qkn.MappingRole)
            .HasColumnName("mapping_role")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(qkn => qkn.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        // Explicit Index for FK
        builder.HasIndex(qkn => new { qkn.CenterId, qkn.NodeId })
            .HasDatabaseName("ix_question_knowledge_nodes_center_id_node_id");

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_question_knowledge_nodes_mapping_role", "mapping_role IN ('Primary', 'Secondary', 'Prerequisite')");
        });

        // Relations
        builder.HasOne(qkn => qkn.Question)
            .WithMany()
            .HasForeignKey(qkn => new { qkn.CenterId, qkn.QuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_question_knowledge_nodes_questions_question");

        builder.HasOne(qkn => qkn.Node)
            .WithMany()
            .HasForeignKey(qkn => new { qkn.CenterId, qkn.NodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_question_knowledge_nodes_knowledge_nodes_node");
    }
}
