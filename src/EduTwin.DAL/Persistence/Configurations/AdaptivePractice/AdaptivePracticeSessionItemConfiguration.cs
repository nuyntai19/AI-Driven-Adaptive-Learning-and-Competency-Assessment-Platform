using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AdaptivePractice;

namespace EduTwin.DAL.Persistence.Configurations.AdaptivePractice;

public class AdaptivePracticeSessionItemConfiguration : IEntityTypeConfiguration<AdaptivePracticeSessionItem>
{
    public void Configure(EntityTypeBuilder<AdaptivePracticeSessionItem> builder)
    {
        builder.ToTable("adaptive_practice_session_items");

        builder.HasKey(i => i.SessionItemId).HasName("pk_adaptive_practice_session_items");

        builder.HasAlternateKey(i => new { i.CenterId, i.SessionItemId })
            .HasName("ux_adaptive_practice_session_items_center_id_session_item_id");

        builder.HasIndex(i => new { i.CenterId, i.SessionId, i.SequenceNumber })
            .HasDatabaseName("ix_adaptive_practice_session_items_center_session_seq");

        builder.Property(i => i.SessionItemId).HasColumnName("session_item_id").HasColumnType("bigint unsigned").ValueGeneratedNever();
        builder.Property(i => i.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(i => i.SessionId).HasColumnName("session_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(i => i.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(i => i.KnowledgeNodeId).HasColumnName("knowledge_node_id").HasColumnType("bigint unsigned").IsRequired();

        builder.Property(i => i.Difficulty).HasColumnName("difficulty").HasColumnType("tinyint unsigned").IsRequired();
        builder.Property(i => i.SequenceNumber).HasColumnName("sequence_number").HasColumnType("int").IsRequired();
        builder.Property(i => i.SelectionReasonCode).HasColumnName("selection_reason_code").HasColumnType("varchar(64)").IsRequired();
        builder.Property(i => i.SelectionReasonText).HasColumnName("selection_reason_text").HasColumnType("text").IsRequired();
        builder.Property(i => i.QuestionVersion).HasColumnName("question_version").HasColumnType("varchar(16)").HasDefaultValue("1.0");

        builder.Property(i => i.PresentedAt).HasColumnName("presented_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(i => i.AnsweredAt).HasColumnName("answered_at").HasColumnType("datetime(6)").IsRequired(false);

        builder.Property(i => i.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(i => i.LastAttemptId).HasColumnName("last_attempt_id").HasColumnType("bigint unsigned").IsRequired(false);
        builder.Property(i => i.HintsUsedCount).HasColumnName("hints_used_count").HasColumnType("int").HasDefaultValue(0);

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)").IsRequired(false);

        // Foreign keys
        builder.HasOne(i => i.Session)
            .WithMany(s => s.Items)
            .HasForeignKey(i => new { i.CenterId, i.SessionId })
            .HasPrincipalKey(s => new { s.CenterId, s.SessionId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Question)
            .WithMany()
            .HasForeignKey(i => new { i.CenterId, i.QuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.KnowledgeNode)
            .WithMany()
            .HasForeignKey(i => new { i.CenterId, i.KnowledgeNodeId })
            .HasPrincipalKey(kn => new { kn.CenterId, kn.NodeId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
