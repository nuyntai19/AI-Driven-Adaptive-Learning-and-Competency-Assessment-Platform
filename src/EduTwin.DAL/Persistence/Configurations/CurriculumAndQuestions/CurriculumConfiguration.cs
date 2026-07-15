using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public class CurriculumConfiguration : IEntityTypeConfiguration<Curriculum>
{
    public void Configure(EntityTypeBuilder<Curriculum> builder)
    {
        builder.ToTable("curriculums");

        // Primary Key
        builder.HasKey(c => c.CurriculumId).HasName("pk_curriculums");
        
        // Alternate Key
        builder.HasAlternateKey(c => new { c.CenterId, c.CurriculumId })
            .HasName("ux_curriculums_center_id_curriculum_id");

        // Properties
        builder.Property(c => c.CurriculumId)
            .HasColumnName("curriculum_id")
            .HasColumnType("varchar(36)");
            
        builder.Property(c => c.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(c => c.TeacherId)
            .HasColumnName("teacher_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(c => c.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(c => c.Title)
            .HasColumnName("title")
            .HasColumnType("varchar(250)")
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(c => c.SourceFile)
            .HasColumnName("source_file")
            .HasColumnType("varchar(500)");

        builder.Property(c => c.ReviewStatus)
            .HasColumnName("review_status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        // MTA Properties
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(c => c.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(c => new { c.CenterId, c.TeacherId, c.ReviewStatus })
            .HasDatabaseName("ix_curriculums_center_id_teacher_id_review_status");

        builder.HasIndex(c => new { c.CenterId, c.SubjectId, c.ReviewStatus })
            .HasDatabaseName("ix_curriculums_center_id_subject_id_review_status");

        // Constraints
        builder.ToTable(t => 
        {
            t.HasCheckConstraint("ck_curriculums_review_status", "review_status IN ('Draft', 'Published', 'Archived')");
        });

        // Relations
        builder.HasOne(c => c.Teacher)
            .WithMany()
            .HasForeignKey(c => new { c.CenterId, c.TeacherId })
            .HasPrincipalKey(t => new { t.CenterId, t.TeacherId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curriculums_teachers_teacher");

        builder.HasOne(c => c.Subject)
            .WithMany()
            .HasForeignKey(c => new { c.CenterId, c.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curriculums_subjects_subject");
    }
}
