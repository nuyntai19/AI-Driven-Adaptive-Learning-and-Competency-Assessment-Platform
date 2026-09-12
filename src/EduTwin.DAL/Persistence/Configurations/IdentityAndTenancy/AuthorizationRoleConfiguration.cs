using EduTwin.DAL.IdentityAndTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Authorization;

public sealed class AuthorizationRoleConfiguration : IEntityTypeConfiguration<AuthorizationRole>
{
    public void Configure(EntityTypeBuilder<AuthorizationRole> builder)
    {
        builder.ToTable("roles", table =>
        {
            table.HasCheckConstraint("ck_roles_status", "`status` IN ('Active', 'Archived')");
            table.HasCheckConstraint("ck_roles_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager', 'PlatformAdmin')");
        });
        builder.HasKey(x => x.RoleId).HasName("pk_roles");
        builder.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("varchar(36)");
        builder.Property(x => x.RoleCode).HasColumnName("role_code").HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.RoleName).HasColumnName("role_name").HasColumnType("varchar(150)").IsRequired();
        builder.Property(x => x.AccountType).HasColumnName("account_type").HasColumnType("varchar(32)").HasConversion<string>().IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("varchar(500)");
        builder.Property(x => x.IsSystemRole).HasColumnName("is_system_role").HasColumnType("tinyint(1)").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("varchar(32)").HasConversion<string>().IsRequired();
        builder.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned")
            .HasDefaultValue(1UL).IsConcurrencyToken().IsRequired();
        builder.HasAlternateKey(x => new { x.CenterId, x.RoleId }).HasName("ux_roles_center_id_role_id");
        builder.HasAlternateKey(x => new { x.CenterId, x.RoleId, x.AccountType })
            .HasName("ux_roles_center_id_role_id_account_type");
        builder.HasIndex(x => new { x.CenterId, x.RoleCode }).IsUnique().HasDatabaseName("ux_roles_center_id_role_code");
        builder.HasIndex(x => new { x.CenterId, x.AccountType, x.Status, x.RoleName })
            .HasDatabaseName("ix_roles_center_account_type_status_role_name");
        builder.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_roles_centers_tenant");
    }
}
