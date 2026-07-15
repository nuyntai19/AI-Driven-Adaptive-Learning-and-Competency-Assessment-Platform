using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Organization.Student>
{
    public void Configure(EntityTypeBuilder<Organization.Student> builder)
    {
        builder.ToTable("students");

        builder.HasKey(s => s.StudentId)
            .HasName("pk_students");

        builder.Property(s => s.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("VARCHAR(36)");

        builder.Property(s => s.FullName)
            .HasColumnName("full_name")
            .HasColumnType("VARCHAR(200)")
            .IsRequired();

        builder.Property(s => s.GradeLevel)
            .HasColumnName("grade_level")
            .HasColumnType("TINYINT UNSIGNED")
            .IsRequired();

        builder.Property(s => s.DateOfBirth)
            .HasColumnName("date_of_birth")
            .HasColumnType("DATE");

        // MTA fields
        builder.Property(s => s.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(s => s.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(s => s.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("TINYINT(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(s => s.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(s => s.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(s => s.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("BIGINT UNSIGNED")
            .HasDefaultValue(1)
            .IsConcurrencyToken()
            .IsRequired();

        // Alternate Key
        builder.HasAlternateKey(s => new { s.CenterId, s.StudentId })
            .HasName("ux_students_center_id_student_id");

        // Indexes
        builder.HasIndex(s => new { s.CenterId, s.GradeLevel })
            .HasDatabaseName("ix_students_center_id_grade_level");

        // CHECK Constraint
        builder.ToTable(t => t.HasCheckConstraint("ck_students_grade_level", "grade_level BETWEEN 10 AND 12"));

        // Relations
        // Tenant-safe FK to users (1-to-1 Profile)
        builder.HasOne(s => s.User)
            .WithOne()
            .HasForeignKey<Organization.Student>(s => new { s.CenterId, s.StudentId })
            .HasPrincipalKey<IdentityAndTenancy.User>(u => new { u.CenterId, u.UserId })
            .HasConstraintName("fk_students_users_profile")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
