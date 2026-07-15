using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Organization.Subject>
{
    public void Configure(EntityTypeBuilder<Organization.Subject> builder)
    {
        builder.ToTable("subjects");

        builder.HasKey(s => s.SubjectId)
            .HasName("pk_subjects");

        builder.Property(s => s.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("VARCHAR(36)");

        builder.Property(s => s.SubjectCode)
            .HasColumnName("subject_code")
            .HasColumnType("VARCHAR(32)")
            .IsRequired();

        builder.Property(s => s.SubjectName)
            .HasColumnName("subject_name")
            .HasColumnType("VARCHAR(100)")
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasColumnType("VARCHAR(500)");

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasColumnType("TINYINT(1)")
            .HasDefaultValue(true)
            .IsRequired();

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
        builder.HasAlternateKey(s => new { s.CenterId, s.SubjectId })
            .HasName("ux_subjects_center_id_subject_id");

        // Indexes
        builder.HasIndex(s => new { s.CenterId, s.SubjectCode })
            .IsUnique()
            .HasDatabaseName("ux_subjects_center_id_subject_code");

        // Relations
        builder.HasOne(s => s.Center)
            .WithMany()
            .HasForeignKey(s => s.CenterId)
            .HasConstraintName("fk_subjects_centers_tenant")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
