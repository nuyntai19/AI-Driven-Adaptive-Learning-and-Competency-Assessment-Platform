using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public sealed class QuestionImageConfiguration : IEntityTypeConfiguration<QuestionImage>
{
    public void Configure(EntityTypeBuilder<QuestionImage> builder)
    {
        builder.ToTable("question_images", t => t.HasCheckConstraint("ck_question_images_size", "OCTET_LENGTH(`data`) BETWEEN 1 AND 2097152"));
        builder.HasKey(x => new { x.CenterId, x.QuestionId }).HasName("pk_question_images");
        builder.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        builder.Property(x => x.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.Data).HasColumnName("data").HasColumnType("mediumblob").IsRequired();
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.HasOne(x => x.Question).WithMany()
            .HasForeignKey(x => new { x.CenterId, x.QuestionId })
            .HasPrincipalKey(x => new { x.CenterId, x.QuestionId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_question_images_questions_question");
    }
}
