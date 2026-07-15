using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class ClassConfiguration : IEntityTypeConfiguration<Organization.Class>
{
    public void Configure(EntityTypeBuilder<Organization.Class> builder)
    {
        builder.ToTable("classes");

        builder.HasKey(c => c.ClassId)
            .HasName("pk_classes");

        builder.Property(c => c.ClassId)
            .HasColumnName("class_id")
            .HasColumnType("VARCHAR(36)");

        builder.Property(c => c.TeacherId)
            .HasColumnName("teacher_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(c => c.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(c => c.ClassName)
            .HasColumnName("class_name")
            .HasColumnType("VARCHAR(150)")
            .IsRequired();

        builder.Property(c => c.AcademicYear)
            .HasColumnName("academic_year")
            .HasColumnType("VARCHAR(20)")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasColumnType("VARCHAR(32)")
            .HasConversion<string>()
            .IsRequired();

        // MTA fields
        builder.Property(c => c.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(c => c.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("TINYINT(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(c => c.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(c => c.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("BIGINT UNSIGNED")
            .HasDefaultValue(1)
            .IsConcurrencyToken()
            .IsRequired();

        // Alternate Key
        builder.HasAlternateKey(c => new { c.CenterId, c.ClassId })
            .HasName("ux_classes_center_id_class_id");

        // Indexes
        builder.HasIndex(c => new { c.CenterId, c.ClassName, c.AcademicYear })
            .IsUnique()
            .HasDatabaseName("ux_classes_center_id_class_name_academic_year");

        builder.HasIndex(c => new { c.CenterId, c.TeacherId, c.Status })
            .HasDatabaseName("ix_classes_center_id_teacher_id_status");

        builder.HasIndex(c => new { c.CenterId, c.SubjectId, c.Status })
            .HasDatabaseName("ix_classes_center_id_subject_id_status");

        // CHECK Constraint
        builder.ToTable(t => t.HasCheckConstraint("ck_classes_status", "status IN ('Active', 'Archived')"));

        // Relations
        // Tenant-safe FK to teachers
        builder.HasOne(c => c.Teacher)
            .WithMany()
            .HasForeignKey(c => new { c.CenterId, c.TeacherId })
            .HasPrincipalKey(t => new { t.CenterId, t.TeacherId })
            .HasConstraintName("fk_classes_teachers_teacher")
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant-safe FK to subjects
        builder.HasOne(c => c.Subject)
            .WithMany()
            .HasForeignKey(c => new { c.CenterId, c.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .HasConstraintName("fk_classes_subjects_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
