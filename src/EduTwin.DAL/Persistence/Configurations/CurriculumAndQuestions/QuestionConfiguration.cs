using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");

        // Primary Key
        builder.HasKey(q => q.QuestionId).HasName("pk_questions");

        // Alternate Key
        builder.HasAlternateKey(q => new { q.CenterId, q.QuestionId })
            .HasName("ux_questions_center_id_question_id");

        // Properties
        builder.Property(q => q.QuestionId)
            .HasColumnName("question_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAdd();

        builder.Property(q => q.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(q => q.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(q => q.PrimaryTopicNodeId)
            .HasColumnName("primary_topic_node_id")
            .HasColumnType("bigint unsigned");

        builder.Property(q => q.CreatedByTeacherId)
            .HasColumnName("created_by_teacher_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(q => q.QuestionType)
            .HasColumnName("question_type")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(q => q.Difficulty)
            .HasColumnName("difficulty")
            .HasColumnType("tinyint unsigned");

        builder.Property(q => q.QuestionText)
            .HasColumnName("question_text")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(q => q.CorrectAnswer)
            .HasColumnName("correct_answer")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(q => q.Solution)
            .HasColumnName("solution")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(q => q.ExpectedReasoning)
            .HasColumnName("expected_reasoning")
            .HasColumnType("longtext");

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        builder.Property(q => q.GradingCriteria)
            .HasColumnName("grading_criteria")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<GradingCriteria>(v, jsonOptions) ?? new GradingCriteria())
            .Metadata.SetValueComparer(new ValueComparer<GradingCriteria>(
                (c1, c2) => JsonSerializer.Serialize(c1, jsonOptions) == JsonSerializer.Serialize(c2, jsonOptions),
                c => JsonSerializer.Serialize(c, jsonOptions).GetHashCode(),
                c => JsonSerializer.Deserialize<GradingCriteria>(JsonSerializer.Serialize(c, jsonOptions), jsonOptions)!
            ));

        builder.Property(q => q.MaxScore)
            .HasColumnName("max_score")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(1.00m);

        builder.Property(q => q.EstimatedTimeSeconds)
            .HasColumnName("estimated_time_seconds")
            .HasColumnType("int unsigned");

        builder.Property(q => q.ReasoningRequired)
            .HasColumnName("reasoning_required")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(true);

        builder.Property(q => q.LanguageCode)
            .HasColumnName("language_code")
            .HasColumnType("varchar(8)")
            .IsRequired();

        builder.Property(q => q.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        // MTA Properties
        builder.Property(q => q.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(q => q.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(q => q.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(q => q.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(q => q.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(q => q.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(q => q.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(q => new { q.CenterId, q.SubjectId, q.PrimaryTopicNodeId, q.Status, q.Difficulty })
            .HasDatabaseName("ix_questions_center_id_subject_id_topic_id_status_difficulty");

        builder.HasIndex(q => new { q.CenterId, q.CreatedByTeacherId, q.Status })
            .HasDatabaseName("ix_questions_center_id_created_by_teacher_id_status");

        // Explicit FK Indexes
        builder.HasIndex(q => new { q.CenterId, q.PrimaryTopicNodeId })
            .HasDatabaseName("ix_questions_center_id_primary_topic_node_id");

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_questions_question_type", "question_type IN ('MultipleChoice', 'ShortAnswer', 'Essay')");
            t.HasCheckConstraint("ck_questions_difficulty", "difficulty BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_questions_max_score", "max_score > 0");
            t.HasCheckConstraint("ck_questions_estimated_time_seconds", "estimated_time_seconds > 0");
            t.HasCheckConstraint("ck_questions_language_code", "language_code IN ('vi', 'en')");
            t.HasCheckConstraint("ck_questions_status", "status IN ('Draft', 'Active', 'Archived')");
        });

        // Relations
        builder.HasOne(q => q.Subject)
            .WithMany()
            .HasForeignKey(q => new { q.CenterId, q.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_questions_subjects_subject");

        builder.HasOne(q => q.PrimaryTopicNode)
            .WithMany()
            .HasForeignKey(q => new { q.CenterId, q.PrimaryTopicNodeId })
            .HasPrincipalKey(n => new { n.CenterId, n.NodeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_questions_knowledge_nodes_primary_topic");

        builder.HasOne(q => q.CreatedByTeacher)
            .WithMany()
            .HasForeignKey(q => new { q.CenterId, q.CreatedByTeacherId })
            .HasPrincipalKey(t => new { t.CenterId, t.TeacherId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_questions_teachers_created_by_teacher");
    }
}
