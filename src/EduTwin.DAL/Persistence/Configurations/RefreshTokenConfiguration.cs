using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<IdentityAndTenancy.RefreshToken>
{
    public void Configure(EntityTypeBuilder<IdentityAndTenancy.RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.RefreshTokenId)
            .HasName("pk_refresh_tokens");

        builder.Property(rt => rt.RefreshTokenId)
            .HasColumnName("refresh_token_id")
            .HasColumnType("BIGINT UNSIGNED")
            .ValueGeneratedOnAdd();

        builder.Property(rt => rt.UserId)
            .HasColumnName("user_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(rt => rt.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("CHAR(64)")
            .IsRequired();

        builder.Property(rt => rt.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(rt => rt.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(rt => rt.ReplacedByTokenId)
            .HasColumnName("replaced_by_token_id")
            .HasColumnType("BIGINT UNSIGNED");

        builder.Property(rt => rt.RevokeReason)
            .HasColumnName("revoke_reason")
            .HasColumnType("VARCHAR(200)");

        builder.Property(rt => rt.CreatedByIp)
            .HasColumnName("created_by_ip")
            .HasColumnType("VARCHAR(64)");

        builder.Property(rt => rt.RevokedByIp)
            .HasColumnName("revoked_by_ip")
            .HasColumnType("VARCHAR(64)");

        // TA fields
        builder.Property(rt => rt.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(rt => rt.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("VARCHAR(36)");

        // Alternate Key for Tenant-safe self-referencing
        builder.HasAlternateKey(rt => new { rt.CenterId, rt.RefreshTokenId })
            .HasName("ux_refresh_tokens_center_id_refresh_token_id");

        // Indexes
        builder.HasIndex(rt => rt.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_refresh_tokens_token_hash");

        builder.HasIndex(rt => new { rt.CenterId, rt.UserId, rt.ExpiresAt })
            .HasDatabaseName("ix_refresh_tokens_center_id_user_id_expires_at");

        // Explicit index for self-referencing FK to avoid uppercase implicit IX_ prefix
        builder.HasIndex(rt => new { rt.CenterId, rt.ReplacedByTokenId })
            .HasDatabaseName("ix_refresh_tokens_center_id_replaced_by_token_id");

        // Relations
        // Tenant-safe FK to users
        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => new { rt.CenterId, rt.UserId })
            .HasPrincipalKey(u => new { u.CenterId, u.UserId })
            .HasConstraintName("fk_refresh_tokens_users_tenant")
            .OnDelete(DeleteBehavior.Restrict);

        // Self FK
        builder.HasOne(rt => rt.ReplacedByToken)
            .WithMany()
            .HasForeignKey(rt => new { rt.CenterId, rt.ReplacedByTokenId })
            .HasPrincipalKey(rt => new { rt.CenterId, rt.RefreshTokenId })
            .HasConstraintName("fk_refresh_tokens_refresh_tokens_replaced_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
