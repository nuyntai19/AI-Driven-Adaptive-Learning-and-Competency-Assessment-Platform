using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class TeacherConfiguration : IEntityTypeConfiguration<Organization.Teacher>
{
    public void Configure(EntityTypeBuilder<Organization.Teacher> builder)
    {
        builder.ToTable("teachers");

        builder.HasKey(t => t.TeacherId)
            .HasName("pk_teachers");

        builder.Property(t => t.TeacherId)
            .HasColumnName("teacher_id")
            .HasColumnType("VARCHAR(36)");

        builder.Property(t => t.Department)
            .HasColumnName("department")
            .HasColumnType("VARCHAR(150)");

        builder.Property(t => t.Bio)
            .HasColumnName("bio")
            .HasColumnType("VARCHAR(500)");

        // MTA fields
        builder.Property(t => t.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(t => t.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(t => t.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("TINYINT(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(t => t.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(t => t.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("BIGINT UNSIGNED")
            .HasDefaultValue(1)
            .IsConcurrencyToken()
            .IsRequired();

        // Alternate Key
        builder.HasAlternateKey(t => new { t.CenterId, t.TeacherId })
            .HasName("ux_teachers_center_id_teacher_id");

        // Relations
        // Tenant-safe FK to users (1-to-1 Profile)
        builder.HasOne(t => t.User)
            .WithOne()
            .HasForeignKey<Organization.Teacher>(t => new { t.CenterId, t.TeacherId })
            .HasPrincipalKey<IdentityAndTenancy.User>(u => new { u.CenterId, u.UserId })
            .HasConstraintName("fk_teachers_users_profile")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
