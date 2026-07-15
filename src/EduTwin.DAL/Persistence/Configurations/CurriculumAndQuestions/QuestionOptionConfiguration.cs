using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable("question_options");

        // Primary Key
        builder.HasKey(o => o.OptionId).HasName("pk_question_options");

        // Properties
        builder.Property(o => o.OptionId)
            .HasColumnName("option_id")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedNever();

        builder.Property(o => o.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(o => o.QuestionId)
            .HasColumnName("question_id")
            .HasColumnType("bigint unsigned");

        builder.Property(o => o.OptionLabel)
            .HasColumnName("option_label")
            .HasColumnType("varchar(8)")
            .IsRequired();

        builder.Property(o => o.OptionText)
            .HasColumnName("option_text")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(o => o.IsCorrect)
            .HasColumnName("is_correct")
            .HasColumnType("tinyint(1)");

        builder.Property(o => o.OrderIndex)
            .HasColumnName("order_index")
            .HasColumnType("int unsigned");

        // MTA Properties
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(o => o.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(o => o.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(o => new { o.CenterId, o.QuestionId, o.OptionLabel })
            .IsUnique()
            .HasDatabaseName("ux_question_options_center_id_question_id_option_label");

        builder.HasIndex(o => new { o.CenterId, o.QuestionId, o.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_question_options_center_id_question_id_order_index");

        // Relations
        builder.HasOne(o => o.Question)
            .WithMany()
            .HasForeignKey(o => new { o.CenterId, o.QuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_question_options_questions_question");
    }
}
