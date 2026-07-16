using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.DAL.Persistence.Configurations.DigitalTwin;

public class StudentSubjectGoalConfiguration : IEntityTypeConfiguration<StudentSubjectGoal>
{
    public void Configure(EntityTypeBuilder<StudentSubjectGoal> builder)
    {
        builder.ToTable("student_subject_goals");

        builder.HasKey(g => g.GoalId).HasName("pk_student_subject_goals");

        builder.HasIndex(g => new { g.CenterId, g.StudentId, g.SubjectId })
            .IsUnique()
            .HasDatabaseName("ux_student_subject_goals_center_id_student_id_subject_id");

        builder.HasIndex(g => new { g.CenterId, g.SubjectId, g.RiskScore })
            .HasDatabaseName("ix_student_subject_goals_center_id_subject_id_risk_score");

        builder.Property(g => g.GoalId).HasColumnName("goal_id").HasColumnType("bigint unsigned").ValueGeneratedNever();
        builder.Property(g => g.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(g => g.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(g => g.TargetScore).HasColumnName("target_score").HasColumnType("decimal(4,2)");
        builder.Property(g => g.RemainingDays).HasColumnName("remaining_days").HasColumnType("int unsigned");
        builder.Property(g => g.CurrentPredictedScore).HasColumnName("current_predicted_score").HasColumnType("decimal(4,2)").HasDefaultValue(0m);
        builder.Property(g => g.RiskScore).HasColumnName("risk_score").HasColumnType("decimal(5,2)").HasDefaultValue(0m);

        builder.Property(g => g.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(g => g.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(g => g.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(g => g.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(g => g.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(g => g.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(g => g.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(g => g.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_student_subject_goals_target_score", "`target_score` BETWEEN 0 AND 10");
            t.HasCheckConstraint("ck_student_subject_goals_current_predicted_score", "`current_predicted_score` BETWEEN 0 AND 10");
            t.HasCheckConstraint("ck_student_subject_goals_risk_score", "`risk_score` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_student_subject_goals_remaining_days", "remaining_days <= 3650");
        });

        builder.HasOne(g => g.Student)
            .WithMany()
            .HasForeignKey(g => new { g.CenterId, g.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_subject_goals_students_student");

        builder.HasOne(g => g.Subject)
            .WithMany()
            .HasForeignKey(g => new { g.CenterId, g.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_subject_goals_subjects_subject");
    }
}
