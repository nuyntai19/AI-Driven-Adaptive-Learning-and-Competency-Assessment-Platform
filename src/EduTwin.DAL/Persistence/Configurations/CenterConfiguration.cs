using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class CenterConfiguration : IEntityTypeConfiguration<Organization.Center>
{
    public void Configure(EntityTypeBuilder<Organization.Center> builder)
    {
        builder.ToTable("centers");

        builder.HasKey(c => c.CenterId)
            .HasName("pk_centers");

        builder.Property(c => c.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)");

        builder.Property(c => c.CenterCode)
            .HasColumnName("center_code")
            .HasColumnType("VARCHAR(32)")
            .IsRequired();

        builder.Property(c => c.CenterName)
            .HasColumnName("center_name")
            .HasColumnType("VARCHAR(200)")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasColumnType("VARCHAR(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(c => c.Timezone)
            .HasColumnName("timezone")
            .HasColumnType("VARCHAR(64)")
            .HasDefaultValue("Asia/Bangkok")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("TINYINT(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(c => c.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("BIGINT UNSIGNED")
            .HasDefaultValue(1)
            .IsConcurrencyToken()
            .IsRequired();

        // Indexes
        builder.HasIndex(c => c.CenterCode)
            .IsUnique()
            .HasDatabaseName("ux_centers_center_code");

        // CHECK Constraint
        builder.ToTable(t => t.HasCheckConstraint("ck_centers_status", "status IN ('Active', 'Suspended')"));
    }
}
