using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class PathProposalConfiguration : IEntityTypeConfiguration<PathProposal>
{
    public void Configure(EntityTypeBuilder<PathProposal> builder)
    {
        builder.ToTable("path_proposals");

        builder.HasKey(p => p.ProposalId).HasName("pk_path_proposals");

        builder.HasAlternateKey(p => new { p.CenterId, p.ProposalId })
            .HasName("ux_path_proposals_center_id_proposal_id");

        builder.HasIndex(p => new { p.CenterId, p.StudentId, p.Status })
            .HasDatabaseName("ix_path_proposals_center_student_status");

        builder.Property(p => p.ProposalId)
            .HasColumnName("proposal_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.GoalId)
            .HasColumnName("goal_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(p => p.StartingBaselineSummary)
            .HasColumnName("starting_baseline_summary")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.CandidatePlan)
            .HasColumnName("candidate_plan")
            .HasColumnType("json")
            .HasConversion(
                v => v.RootElement.GetRawText(),
                v => JsonDocument.Parse(v, default))
            .IsRequired();

        builder.Property(p => p.FeasibilityStatus)
            .HasColumnName("feasibility_status")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Feasible")
            .IsRequired();

        builder.Property(p => p.FeasibilityMessage)
            .HasColumnName("feasibility_message")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.EstimatedWeeks)
            .HasColumnName("estimated_weeks")
            .HasColumnType("int unsigned")
            .HasDefaultValue(4u)
            .IsRequired();

        builder.Property(p => p.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Pending")
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(p => p.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(p => p.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(p => p.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .IsConcurrencyToken()
            .HasDefaultValue(1ul)
            .IsRequired();

        builder.HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => new { p.CenterId, p.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .HasConstraintName("fk_path_proposals_students_student")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Subject)
            .WithMany()
            .HasForeignKey(p => new { p.CenterId, p.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .HasConstraintName("fk_path_proposals_subjects_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Goal)
            .WithMany()
            .HasForeignKey(p => new { p.CenterId, p.GoalId })
            .HasPrincipalKey(g => new { g.CenterId, g.GoalId })
            .HasConstraintName("fk_path_proposals_learning_goals_goal")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
