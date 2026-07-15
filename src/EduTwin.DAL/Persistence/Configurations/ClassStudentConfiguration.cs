using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class ClassStudentConfiguration : IEntityTypeConfiguration<Organization.ClassStudent>
{
    public void Configure(EntityTypeBuilder<Organization.ClassStudent> builder)
    {
        builder.ToTable("class_students");

        builder.HasKey(cs => new { cs.CenterId, cs.ClassId, cs.StudentId })
            .HasName("pk_class_students");

        builder.Property(cs => cs.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(cs => cs.ClassId)
            .HasColumnName("class_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(cs => cs.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(cs => cs.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(cs => cs.Status)
            .HasColumnName("status")
            .HasColumnType("VARCHAR(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(cs => cs.RemovedAt)
            .HasColumnName("removed_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(cs => cs.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("VARCHAR(36)");

        // Indexes
        builder.HasIndex(cs => new { cs.CenterId, cs.StudentId, cs.Status })
            .HasDatabaseName("ix_class_students_center_id_student_id_status");

        // CHECK Constraint
        builder.ToTable(t => t.HasCheckConstraint("ck_class_students_status", "status IN ('Active', 'Removed')"));

        // Relations
        // Tenant-safe FK to classes
        builder.HasOne(cs => cs.Class)
            .WithMany(c => c.ClassStudents)
            .HasForeignKey(cs => new { cs.CenterId, cs.ClassId })
            .HasPrincipalKey(c => new { c.CenterId, c.ClassId })
            .HasConstraintName("fk_class_students_classes_class")
            .OnDelete(DeleteBehavior.Restrict);

        // Tenant-safe FK to students
        builder.HasOne(cs => cs.Student)
            .WithMany()
            .HasForeignKey(cs => new { cs.CenterId, cs.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .HasConstraintName("fk_class_students_students_student")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
