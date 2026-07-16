using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public class AIAnalysisJobConfiguration : IEntityTypeConfiguration<AIAnalysisJob>
{
    public void Configure(EntityTypeBuilder<AIAnalysisJob> builder)
    {
        builder.ToTable("ai_analysis_jobs");

        builder.HasKey(j => j.AnalysisJobId).HasName("pk_ai_analysis_jobs");

        builder.HasIndex(j => new { j.CenterId, j.AttemptId })
            .IsUnique()
            .HasDatabaseName("ux_ai_analysis_jobs_center_id_attempt_id");

        builder.HasIndex(j => new { j.Status, j.AvailableAt, j.LeaseUntil })
            .HasDatabaseName("ix_ai_analysis_jobs_status_available_at_lease_until");

        builder.HasIndex(j => new { j.CenterId, j.Status, j.CreatedAt })
            .HasDatabaseName("ix_ai_analysis_jobs_center_id_status_created_at");

        builder.Property(j => j.AnalysisJobId).HasColumnName("analysis_job_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(j => j.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(j => j.AttemptId).HasColumnName("attempt_id").HasColumnType("bigint unsigned");

        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(j => j.RetryCount).HasColumnName("retry_count").HasColumnType("tinyint unsigned").HasDefaultValue(0);
        builder.Property(j => j.AvailableAt).HasColumnName("available_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(j => j.StartedAt).HasColumnName("started_at").HasColumnType("datetime(6)");
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at").HasColumnType("datetime(6)");
        builder.Property(j => j.LeaseOwner).HasColumnName("lease_owner").HasColumnType("varchar(100)");
        builder.Property(j => j.LeaseUntil).HasColumnName("lease_until").HasColumnType("datetime(6)");
        builder.Property(j => j.LastErrorCode).HasColumnName("last_error_code").HasColumnType("varchar(100)");
        builder.Property(j => j.LastErrorMessage).HasColumnName("last_error_message").HasColumnType("varchar(1000)");
        builder.Property(j => j.CorrelationId).HasColumnName("correlation_id").HasColumnType("varchar(64)").IsRequired();

        builder.Property(j => j.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(j => j.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(j => j.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_ai_analysis_jobs_retry_count", "`retry_count` BETWEEN 0 AND 1");
            t.HasCheckConstraint("ck_ai_analysis_jobs_status", "`status` IN ('Pending', 'Processing', 'Completed', 'FallbackCompleted', 'FailedTerminal')");
        });

        builder.HasOne(j => j.Attempt)
            .WithMany()
            .HasForeignKey(j => new { j.CenterId, j.AttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ai_analysis_jobs_attempts_attempt");
    }
}
