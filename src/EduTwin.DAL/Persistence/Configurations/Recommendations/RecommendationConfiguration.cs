using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("recommendations");

        builder.HasKey(r => r.RecommendationId).HasName("pk_recommendations");

        builder.HasIndex(r => new { r.CenterId, r.StudentId, r.SubjectId, r.Status, r.GeneratedAt })
            .HasDatabaseName("ix_recommendations_center_student_subject_status_generated_at");

        builder.HasIndex(r => new { r.CenterId, r.SubjectId })
            .HasDatabaseName("ix_recommendations_center_id_subject_id");

        builder.HasIndex(r => new { r.CenterId, r.TopicNodeId })
            .HasDatabaseName("ix_recommendations_center_id_topic_node_id");

        builder.HasIndex(r => new { r.CenterId, r.QuestionId })
            .HasDatabaseName("ix_recommendations_center_id_question_id");

        builder.Property(r => r.RecommendationId).HasColumnName("recommendation_id").HasColumnType("bigint unsigned").ValueGeneratedNever();
        builder.Property(r => r.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(r => r.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(r => r.TopicNodeId).HasColumnName("topic_node_id").HasColumnType("bigint unsigned");
        builder.Property(r => r.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned");

        builder.Property(r => r.RecommendationType)
            .HasColumnName("recommendation_type")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.OpportunityScore).HasColumnName("opportunity_score").HasColumnType("decimal(5,2)");
        builder.Property(r => r.CalculationVersion).HasColumnName("calculation_version").HasColumnType("varchar(20)").IsRequired();
        builder.Property(r => r.CalculationBreakdown)
            .HasColumnName("calculation_breakdown")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(
                v => v.RootElement.ToString(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));
        builder.Property(r => r.Explanation).HasColumnName("explanation").HasColumnType("varchar(1000)").IsRequired();
        builder.Property(r => r.SourceAttemptId).HasColumnName("source_attempt_id").HasColumnType("bigint unsigned");

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(r => r.GeneratedAt).HasColumnName("generated_at").HasColumnType("datetime(6)");
        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").HasColumnType("datetime(6)");

        builder.Property(r => r.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(r => r.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_recommendations_recommendation_type", "`recommendation_type` IN ('TopicAndQuestion', 'LinearFallback')");
            t.HasCheckConstraint("ck_recommendations_status", "`status` IN ('Active', 'Accepted', 'Dismissed', 'Superseded')");
            t.HasCheckConstraint("ck_recommendations_opportunity_score", "`opportunity_score` IS NULL OR `opportunity_score` BETWEEN 0 AND 100");
        });

        builder.HasOne(r => r.Student)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recommendations_students_student");

        builder.HasOne(r => r.Subject)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recommendations_subjects_subject");

        builder.HasOne(r => r.TopicNode)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.TopicNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recommendations_knowledge_nodes_topic_node");

        builder.HasOne(r => r.Question)
            .WithMany()
            .HasForeignKey(r => new { r.CenterId, r.QuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_recommendations_questions_question");
    }
}
