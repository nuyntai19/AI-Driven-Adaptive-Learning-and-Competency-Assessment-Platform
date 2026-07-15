using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<IdentityAndTenancy.User>
{
    public void Configure(EntityTypeBuilder<IdentityAndTenancy.User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.UserId)
            .HasName("pk_users");

        builder.Property(u => u.UserId)
            .HasColumnName("user_id")
            .HasColumnType("VARCHAR(36)");

        builder.Property(u => u.Username)
            .HasColumnName("username")
            .HasColumnType("VARCHAR(100)")
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("VARCHAR(500)")
            .IsRequired();

        builder.Property(u => u.RoleName)
            .HasColumnName("role_name")
            .HasColumnType("VARCHAR(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .HasColumnType("VARCHAR(200)")
            .IsRequired();

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasColumnType("VARCHAR(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(u => u.LastLoginAt)
            .HasColumnName("last_login_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(u => u.AuthVersion)
            .HasColumnName("auth_version")
            .HasColumnType("INT UNSIGNED")
            .HasDefaultValue(1)
            .IsRequired();

        // MTA fields
        builder.Property(u => u.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(u => u.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(u => u.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(u => u.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("TINYINT(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(u => u.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(u => u.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(u => u.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("BIGINT UNSIGNED")
            .HasDefaultValue(1)
            .IsConcurrencyToken()
            .IsRequired();

        // Alternate/Unique Key for Tenant-safe relationships
        builder.HasAlternateKey(u => new { u.CenterId, u.UserId })
            .HasName("ux_users_center_id_user_id"); // This creates ux_users_center_id_user_id (center_id, user_id)

        // Indexes
        builder.HasIndex(u => new { u.CenterId, u.Username })
            .IsUnique()
            .HasDatabaseName("ux_users_center_id_username");

        builder.HasIndex(u => new { u.CenterId, u.RoleName, u.Status })
            .HasDatabaseName("ix_users_center_id_role_name_status");

        // CHECK Constraints
        builder.ToTable(t => t.HasCheckConstraint("ck_users_role_name", "role_name IN ('Student', 'Teacher', 'CenterManager')"));
        builder.ToTable(t => t.HasCheckConstraint("ck_users_status", "status IN ('Active', 'Locked', 'Disabled')"));

        // Relations
        builder.HasOne(u => u.Center)
            .WithMany()
            .HasForeignKey(u => u.CenterId)
            .HasConstraintName("fk_users_centers_tenant")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
