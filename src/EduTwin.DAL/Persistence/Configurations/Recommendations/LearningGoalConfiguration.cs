using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class LearningGoalConfiguration : IEntityTypeConfiguration<LearningGoal>
{
    public void Configure(EntityTypeBuilder<LearningGoal> builder)
    {
        builder.ToTable("learning_goals");
        builder.Property(g => g.TargetScore).HasColumnName("target_score").HasColumnType("decimal(4,2)");
        builder.Property(g => g.StartWithPlacement).HasColumnName("start_with_placement").HasDefaultValue(false);
        builder.Property(g => g.ScheduleSettings).HasColumnName("schedule_settings").HasColumnType("json")
            .HasConversion(v => v != null ? v.RootElement.GetRawText() : null, v => v != null ? JsonDocument.Parse(v, default) : null);

        builder.HasKey(g => g.GoalId).HasName("pk_learning_goals");

        builder.HasAlternateKey(g => new { g.CenterId, g.GoalId })
            .HasName("ux_learning_goals_center_id_goal_id");

        builder.HasIndex(g => new { g.CenterId, g.StudentId, g.SubjectId, g.Status })
            .HasDatabaseName("ix_learning_goals_center_student_subject_status");

        builder.Property(g => g.GoalId)
            .HasColumnName("goal_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(g => g.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(g => g.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(g => g.SubjectId)
            .HasColumnName("subject_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(g => g.ClassId)
            .HasColumnName("class_id")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(g => g.GoalType)
            .HasColumnName("goal_type")
            .HasColumnType("varchar(64)")
            .IsRequired();

        builder.Property(g => g.Title)
            .HasColumnName("title")
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(g => g.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(g => g.TargetSkillIds)
            .HasColumnName("target_skill_ids")
            .HasColumnType("json")
            .HasConversion(
                v => v != null ? v.RootElement.GetRawText() : null,
                v => v != null ? JsonDocument.Parse(v, default) : null)
            .IsRequired(false);

        builder.Property(g => g.DeadlineDate)
            .HasColumnName("deadline_date")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(g => g.AvailableMinutesPerDay)
            .HasColumnName("available_minutes_per_day")
            .HasColumnType("int unsigned")
            .HasDefaultValue(15u)
            .IsRequired();

        builder.Property(g => g.AvailableDaysPerWeek)
            .HasColumnName("available_days_per_week")
            .HasColumnType("int unsigned")
            .HasDefaultValue(5u)
            .IsRequired();

        builder.Property(g => g.PreferredPace)
            .HasColumnName("preferred_pace")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Moderate")
            .IsRequired();

        builder.Property(g => g.PreferredMode)
            .HasColumnName("preferred_mode")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Balanced")
            .IsRequired();

        builder.Property(g => g.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasDefaultValue("Active")
            .IsRequired();

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(g => g.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(g => g.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(g => g.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(g => g.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("tinyint(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(g => g.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("datetime(6)")
            .IsRequired(false);

        builder.Property(g => g.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("varchar(36)")
            .IsRequired(false);

        builder.Property(g => g.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("bigint unsigned")
            .IsConcurrencyToken()
            .HasDefaultValue(1ul)
            .IsRequired();

        builder.HasOne(g => g.Student)
            .WithMany()
            .HasForeignKey(g => new { g.CenterId, g.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .HasConstraintName("fk_learning_goals_students_student")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Subject)
            .WithMany()
            .HasForeignKey(g => new { g.CenterId, g.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .HasConstraintName("fk_learning_goals_subjects_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Class)
            .WithMany()
            .HasForeignKey(g => new { g.CenterId, g.ClassId })
            .HasPrincipalKey(c => new { c.CenterId, c.ClassId })
            .HasConstraintName("fk_learning_goals_classes_class")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
