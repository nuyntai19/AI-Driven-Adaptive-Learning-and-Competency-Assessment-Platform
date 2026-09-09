using EduTwin.DAL.IdentityAndTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Authorization;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions", table =>
            table.HasCheckConstraint("ck_role_permissions_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager')"));
        builder.HasKey(x => new { x.CenterId, x.RoleId, x.PermissionId }).HasName("pk_role_permissions");
        builder.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        builder.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("varchar(36)");
        builder.Property(x => x.PermissionId).HasColumnName("permission_id").HasColumnType("varchar(36)");
        builder.Property(x => x.AccountType).HasColumnName("account_type").HasColumnType("varchar(32)").HasConversion<string>();
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.GrantedByUserId).HasColumnName("granted_by_user_id").HasColumnType("varchar(36)");
        builder.HasIndex(x => new { x.CenterId, x.PermissionId, x.RoleId })
            .HasDatabaseName("ix_role_permissions_center_permission_role");
        builder.HasIndex(x => new { x.CenterId, x.RoleId, x.AccountType })
            .HasDatabaseName("ix_role_permissions_center_role_account_type");
        builder.HasIndex(x => new { x.PermissionId, x.AccountType })
            .HasDatabaseName("ix_role_permissions_permission_account_type");
        builder.HasIndex(x => new { x.CenterId, x.GrantedByUserId })
            .HasDatabaseName("ix_role_permissions_center_granted_by_user");
        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions)
            .HasForeignKey(x => new { x.CenterId, x.RoleId, x.AccountType })
            .HasPrincipalKey(x => new { x.CenterId, x.RoleId, x.AccountType })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_role_permissions_roles_account_type");
        builder.HasOne(x => x.PermissionAccountType).WithMany()
            .HasForeignKey(x => new { x.PermissionId, x.AccountType })
            .HasPrincipalKey(x => new { x.PermissionId, x.AccountType })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_role_permissions_permission_account_types");
        builder.HasOne(x => x.GrantedByUser).WithMany()
            .HasForeignKey(x => new { x.CenterId, x.GrantedByUserId })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_role_permissions_users_granted_by");
    }
}
