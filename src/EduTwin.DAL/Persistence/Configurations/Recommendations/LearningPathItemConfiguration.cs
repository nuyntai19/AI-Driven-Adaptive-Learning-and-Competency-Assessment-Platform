using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class LearningPathItemConfiguration : IEntityTypeConfiguration<LearningPathItem>
{
    public void Configure(EntityTypeBuilder<LearningPathItem> builder)
    {
        builder.ToTable("learning_path_items");

        builder.HasKey(i => i.LearningPathItemId).HasName("pk_learning_path_items");

        builder.HasIndex(i => new { i.CenterId, i.LearningPathId, i.RankOrder })
            .IsUnique()
            .HasDatabaseName("ux_learning_path_items_center_id_learning_path_id_rank_order");

        builder.HasIndex(i => new { i.CenterId, i.LearningPathId, i.TopicNodeId })
            .IsUnique()
            .HasDatabaseName("ux_learning_path_items_center_id_learning_path_id_topic_node_id");

        builder.HasIndex(i => new { i.CenterId, i.TopicNodeId })
            .HasDatabaseName("ix_learning_path_items_center_id_topic_node_id");

        builder.HasIndex(i => new { i.CenterId, i.RecommendedQuestionId })
            .HasDatabaseName("ix_learning_path_items_center_id_recommended_question_id");

        builder.Property(i => i.LearningPathItemId).HasColumnName("learning_path_item_id").HasColumnType("bigint unsigned").ValueGeneratedNever();
        builder.Property(i => i.LearningPathId).HasColumnName("learning_path_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(i => i.TopicNodeId).HasColumnName("topic_node_id").HasColumnType("bigint unsigned");
        builder.Property(i => i.RecommendedQuestionId).HasColumnName("recommended_question_id").HasColumnType("bigint unsigned");
        builder.Property(i => i.RankOrder).HasColumnName("rank_order").HasColumnType("int unsigned");
        builder.Property(i => i.OpportunityScore).HasColumnName("opportunity_score").HasColumnType("decimal(5,2)");
        builder.Property(i => i.Reason).HasColumnName("reason").HasColumnType("varchar(1000)").IsRequired();

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(i => i.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(i => i.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(i => i.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_learning_path_items_status", "`status` IN ('Pending', 'Current', 'Completed', 'Skipped')");
            t.HasCheckConstraint("ck_learning_path_items_opportunity_score", "`opportunity_score` IS NULL OR `opportunity_score` BETWEEN 0 AND 100");
        });

        builder.HasOne(i => i.LearningPath)
            .WithMany(l => l.Items)
            .HasForeignKey(i => new { i.CenterId, i.LearningPathId })
            .HasPrincipalKey(l => new { l.CenterId, l.LearningPathId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_items_learning_paths_learning_path");

        builder.HasOne(i => i.TopicNode)
            .WithMany()
            .HasForeignKey(i => new { i.CenterId, i.TopicNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_items_knowledge_nodes_topic_node");

        builder.HasOne(i => i.RecommendedQuestion)
            .WithMany()
            .HasForeignKey(i => new { i.CenterId, i.RecommendedQuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_path_items_questions_recommended_question");
    }
}
