using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public sealed class AIAnalysisCheckpointConfiguration : IEntityTypeConfiguration<AIAnalysisCheckpoint>
{
    public void Configure(EntityTypeBuilder<AIAnalysisCheckpoint> b)
    {
        b.ToTable("ai_analysis_checkpoints");
        b.HasKey(x => new { x.CenterId, x.AttemptId });
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned");
        b.Property(x => x.RequestFingerprint).HasColumnName("request_fingerprint").HasColumnType("varchar(64)").IsRequired();
        b.Property(x => x.ResponseJson).HasColumnName("response_json").HasColumnType("longtext").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();
        b.HasOne<Attempt>().WithMany().HasForeignKey(x => new { x.CenterId, x.AttemptId })
            .HasPrincipalKey(x => new { x.CenterId, x.AttemptId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AIStudentPostProcessingJobConfiguration : IEntityTypeConfiguration<AIStudentPostProcessingJob>
{
    public void Configure(EntityTypeBuilder<AIStudentPostProcessingJob> b)
    {
        b.ToTable("ai_student_post_processing_jobs");
        b.HasKey(x => new { x.CenterId, x.StudentId, x.SubjectId, x.AssignmentScopeId });
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)");
        b.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)");
        b.Property(x => x.AssignmentScopeId).HasColumnName("assignment_scope_id").HasColumnType("varchar(36)");
        b.Property(x => x.Revision).HasColumnName("revision").HasColumnType("bigint unsigned");
        b.Property(x => x.ProcessedRevision).HasColumnName("processed_revision").HasColumnType("bigint unsigned");
        b.Property(x => x.SourceAttemptId).HasColumnName("source_attempt_id").HasColumnType("bigint unsigned");
        b.Property(x => x.TriggerAt).HasColumnName("trigger_at").HasColumnType("datetime(6)");
        b.Property(x => x.AvailableAt).HasColumnName("available_at").HasColumnType("datetime(6)");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasColumnType("varchar(100)");
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until").HasColumnType("datetime(6)");
        b.Property(x => x.FailureCount).HasColumnName("failure_count");
        b.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasColumnType("varchar(100)");
        b.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();
        b.HasIndex(x => new { x.CenterId, x.AvailableAt, x.LeaseUntil });
        b.HasOne<EduTwin.DAL.Organization.Student>().WithMany().HasForeignKey(x => new { x.CenterId, x.StudentId })
            .HasPrincipalKey(x => new { x.CenterId, x.StudentId }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AIProviderQuotaStateConfiguration : IEntityTypeConfiguration<AIProviderQuotaState>
{
    public void Configure(EntityTypeBuilder<AIProviderQuotaState> b)
    {
        b.ToTable("ai_provider_quota_states");
        b.HasKey(x => x.PoolId);
        b.Property(x => x.PoolId).HasColumnName("pool_id").HasColumnType("varchar(64)");
        b.Property(x => x.StateJson).HasColumnName("state_json").HasColumnType("longtext").IsRequired();
        b.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();
    }
}
