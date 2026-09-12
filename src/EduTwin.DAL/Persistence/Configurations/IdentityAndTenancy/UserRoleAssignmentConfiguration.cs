using EduTwin.DAL.IdentityAndTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Authorization;

public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("user_roles", table =>
        {
            table.HasCheckConstraint("ck_user_roles_status", "`status` IN ('Active', 'Revoked')");
            table.HasCheckConstraint("ck_user_roles_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager', 'PlatformAdmin')");
        });
        builder.HasKey(x => new { x.CenterId, x.UserId, x.RoleId }).HasName("pk_user_roles");
        builder.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("varchar(36)");
        builder.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("varchar(36)");
        builder.Property(x => x.AccountType).HasColumnName("account_type").HasColumnType("varchar(32)").HasConversion<string>();
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("varchar(32)").HasConversion<string>();
        builder.Property(x => x.AssignedAt).HasColumnName("assigned_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.AssignedByUserId).HasColumnName("assigned_by_user_id").HasColumnType("varchar(36)");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("datetime(6)");
        builder.Property(x => x.RevokedByUserId).HasColumnName("revoked_by_user_id").HasColumnType("varchar(36)");
        builder.Property(x => x.RevokeReason).HasColumnName("revoke_reason").HasColumnType("varchar(500)");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned")
            .HasDefaultValue(1UL).IsConcurrencyToken().IsRequired();
        builder.HasIndex(x => new { x.CenterId, x.RoleId, x.Status, x.UserId })
            .HasDatabaseName("ix_user_roles_center_role_status_user");
        builder.HasIndex(x => new { x.CenterId, x.UserId, x.Status })
            .HasDatabaseName("ix_user_roles_center_user_status");
        builder.HasIndex(x => new { x.CenterId, x.UserId, x.AccountType })
            .HasDatabaseName("ix_user_roles_center_user_account_type");
        builder.HasIndex(x => new { x.CenterId, x.RoleId, x.AccountType })
            .HasDatabaseName("ix_user_roles_center_role_account_type");
        builder.HasIndex(x => new { x.CenterId, x.AssignedByUserId })
            .HasDatabaseName("ix_user_roles_center_assigned_by_user");
        builder.HasIndex(x => new { x.CenterId, x.RevokedByUserId })
            .HasDatabaseName("ix_user_roles_center_revoked_by_user");
        builder.HasOne(x => x.User).WithMany(x => x.RoleAssignments)
            .HasForeignKey(x => new { x.CenterId, x.UserId, x.AccountType })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId, x.RoleName })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_roles_users_account_type");
        builder.HasOne(x => x.Role).WithMany(x => x.UserAssignments)
            .HasForeignKey(x => new { x.CenterId, x.RoleId, x.AccountType })
            .HasPrincipalKey(x => new { x.CenterId, x.RoleId, x.AccountType })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_roles_roles_account_type");
        builder.HasOne(x => x.AssignedByUser).WithMany()
            .HasForeignKey(x => new { x.CenterId, x.AssignedByUserId })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_roles_users_assigned_by");
        builder.HasOne(x => x.RevokedByUser).WithMany()
            .HasForeignKey(x => new { x.CenterId, x.RevokedByUserId })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_user_roles_users_revoked_by");
    }
}
