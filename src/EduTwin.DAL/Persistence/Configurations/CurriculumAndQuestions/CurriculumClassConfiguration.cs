using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public class CurriculumClassConfiguration : IEntityTypeConfiguration<CurriculumClass>
{
    public void Configure(EntityTypeBuilder<CurriculumClass> builder)
    {
        builder.ToTable("curriculum_classes");

        // Primary Key
        builder.HasKey(cc => new { cc.CenterId, cc.CurriculumId, cc.ClassId }).HasName("pk_curriculum_classes");
        
        // Properties
        builder.Property(cc => cc.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)");

        builder.Property(cc => cc.CurriculumId)
            .HasColumnName("curriculum_id")
            .HasColumnType("varchar(36)");

        builder.Property(cc => cc.ClassId)
            .HasColumnName("class_id")
            .HasColumnType("varchar(36)");

        builder.Property(cc => cc.AssignedAt)
            .HasColumnName("assigned_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(cc => cc.AssignedBy)
            .HasColumnName("assigned_by")
            .HasColumnType("varchar(36)")
            .IsRequired();

        // Explicit index for FKs
        builder.HasIndex(cc => new { cc.CenterId, cc.ClassId })
            .HasDatabaseName("ix_curriculum_classes_center_id_class_id");

        // Relations
        builder.HasOne(cc => cc.Curriculum)
            .WithMany()
            .HasForeignKey(cc => new { cc.CenterId, cc.CurriculumId })
            .HasPrincipalKey(c => new { c.CenterId, c.CurriculumId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curriculum_classes_curriculums_curriculum");

        builder.HasOne(cc => cc.Class)
            .WithMany()
            .HasForeignKey(cc => new { cc.CenterId, cc.ClassId })
            .HasPrincipalKey(c => new { c.CenterId, c.ClassId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_curriculum_classes_classes_class");
    }
}
