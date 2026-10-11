using EduTwin.DAL.PlatformOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.PlatformOperations;

public sealed class PlatformAiCallConfiguration : IEntityTypeConfiguration<PlatformAiCall>
{
    public void Configure(EntityTypeBuilder<PlatformAiCall> b)
    {
        b.ToTable("platform_ai_calls"); b.HasKey(x => x.CallId);
        b.Property(x => x.CallId).HasColumnName("call_id").HasColumnType("varchar(36)");
        b.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("datetime(6)");
        b.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(16);
        b.Property(x => x.Model).HasColumnName("model").HasMaxLength(120);
        b.Property(x => x.PoolAlias).HasColumnName("pool_alias").HasMaxLength(32);
        b.Property(x => x.Stage).HasColumnName("stage").HasMaxLength(32);
        b.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(32);
        b.Property(x => x.DurationMs).HasColumnName("duration_ms");
        b.Property(x => x.InputTokens).HasColumnName("input_tokens");
        b.Property(x => x.OutputTokens).HasColumnName("output_tokens");
        b.Property(x => x.ThinkingTokens).HasColumnName("thinking_tokens");
        b.Property(x => x.CachedTokens).HasColumnName("cached_tokens");
        b.Property(x => x.TotalTokens).HasColumnName("total_tokens");
        b.Property(x => x.PriceVersion).HasColumnName("price_version").HasMaxLength(80);
        b.Property(x => x.EstimatedUsd).HasColumnName("estimated_usd").HasPrecision(18, 8);
        b.HasIndex(x => new { x.StartedAt, x.Provider, x.Model });
        b.ToTable(t => t.HasCheckConstraint("ck_platform_ai_call_duration", "duration_ms >= 0"));
    }
}

public sealed class PlatformOpsSnapshotConfiguration : IEntityTypeConfiguration<PlatformOpsSnapshot>
{
    public void Configure(EntityTypeBuilder<PlatformOpsSnapshot> b)
    {
        b.ToTable("platform_ops_snapshots"); b.HasKey(x => x.SnapshotKey);
        b.Property(x => x.SnapshotKey).HasColumnName("snapshot_key").HasMaxLength(80);
        b.Property(x => x.CapturedAt).HasColumnName("captured_at").HasColumnType("datetime(6)");
        b.Property(x => x.Payload).HasColumnName("payload").HasColumnType("json");
    }
}

public sealed class PlatformOpsAlertConfiguration : IEntityTypeConfiguration<PlatformOpsAlert>
{
    public void Configure(EntityTypeBuilder<PlatformOpsAlert> b)
    {
        b.ToTable("platform_ops_alerts"); b.HasKey(x => x.AlertKey);
        b.Property(x => x.AlertKey).HasColumnName("alert_key").HasMaxLength(80);
        b.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at").HasColumnType("datetime(6)");
        b.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasColumnType("datetime(6)");
        b.Property(x => x.NotifiedAt).HasColumnName("notified_at").HasColumnType("datetime(6)");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.EpisodeId).HasColumnName("episode_id").HasColumnType("varchar(36)");
        b.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").IsConcurrencyToken();
    }
}
