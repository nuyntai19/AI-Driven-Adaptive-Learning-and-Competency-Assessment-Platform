using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Authorization;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", table =>
            table.HasCheckConstraint("ck_permissions_status", "`status` IN ('Active', 'Deprecated')"));
        builder.HasKey(x => x.PermissionId).HasName("pk_permissions");
        builder.Property(x => x.PermissionId).HasColumnName("permission_id").HasColumnType("varchar(36)");
        builder.Property(x => x.PermissionCode).HasColumnName("permission_code").HasColumnType("varchar(100)").IsRequired();
        builder.Property(x => x.ModuleName).HasColumnName("module_name").HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.ResourceName).HasColumnName("resource_name").HasColumnType("varchar(64)").IsRequired();
        builder.Property(x => x.ActionName).HasColumnName("action_name").HasColumnType("varchar(32)").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("varchar(500)").IsRequired();
        builder.Property(x => x.IsSensitive).HasColumnName("is_sensitive").HasColumnType("tinyint(1)").IsRequired();
        builder.Property(x => x.IsDelegable).HasColumnName("is_delegable").HasColumnType("tinyint(1)").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("varchar(32)").HasConversion<string>().IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.HasIndex(x => x.PermissionCode).IsUnique().HasDatabaseName("ux_permissions_permission_code");
        builder.HasIndex(x => new { x.ModuleName, x.ResourceName, x.ActionName, x.Status })
            .HasDatabaseName("ix_permissions_module_resource_action_status");
        builder.HasData(AuthorizationPermissionCatalog.CreatePermissions());
    }
}
