using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.Authorization;

public sealed class PermissionAccountTypeConfiguration : IEntityTypeConfiguration<PermissionAccountType>
{
    public void Configure(EntityTypeBuilder<PermissionAccountType> builder)
    {
        builder.ToTable("permission_account_types", table =>
            table.HasCheckConstraint("ck_permission_account_types_account_type", "`account_type` IN ('Student', 'Teacher', 'CenterManager')"));
        builder.HasKey(x => new { x.PermissionId, x.AccountType }).HasName("pk_permission_account_types");
        builder.Property(x => x.PermissionId).HasColumnName("permission_id").HasColumnType("varchar(36)");
        builder.Property(x => x.AccountType).HasColumnName("account_type").HasColumnType("varchar(32)").HasConversion<string>();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.HasIndex(x => new { x.AccountType, x.PermissionId })
            .HasDatabaseName("ix_permission_account_types_account_type_permission_id");
        builder.HasOne(x => x.Permission)
            .WithMany(x => x.AllowedAccountTypes)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_permission_account_types_permissions");
        builder.HasData(AuthorizationPermissionCatalog.CreateAccountTypeMappings());
    }
}
