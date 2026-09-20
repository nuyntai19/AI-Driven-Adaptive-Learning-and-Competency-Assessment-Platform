using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class StudentLearningPathPreferenceConfiguration : IEntityTypeConfiguration<StudentLearningPathPreference>
{
    public void Configure(EntityTypeBuilder<StudentLearningPathPreference> builder)
    {
        builder.ToTable("student_learning_path_preferences");

        builder.HasKey(p => p.PreferenceId).HasName("pk_student_learning_path_preferences");

        builder.HasAlternateKey(p => new { p.CenterId, p.PreferenceId })
            .HasName("ux_slp_pref_center_pref_id");

        builder.HasIndex(p => new { p.CenterId, p.StudentId, p.SubjectId })
            .IsUnique()
            .HasDatabaseName("ux_slp_pref_center_student_subject");

        builder.Property(p => p.PreferenceId).HasColumnName("preference_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(p => p.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(p => p.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(p => p.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();

        builder.Property(p => p.SelfAssessedLevel).HasColumnName("self_assessed_level").HasColumnType("varchar(32)").IsRequired();

        builder.Property(p => p.WeakTopicNodeIds)
            .HasColumnName("weak_topic_node_ids")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(
                v => v.RootElement.ToString(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));

        builder.Property(p => p.FocusTopicNodeIds)
            .HasColumnName("focus_topic_node_ids")
            .HasColumnType("json")
            .IsRequired()
            .HasConversion(
                v => v.RootElement.ToString(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));

        builder.Property(p => p.GoalType).HasColumnName("goal_type").HasColumnType("varchar(32)").IsRequired();
        builder.Property(p => p.TargetMastery).HasColumnName("target_mastery").HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(p => p.TargetWeeks).HasColumnName("target_weeks").HasColumnType("int").IsRequired();
        builder.Property(p => p.MinutesPerDay).HasColumnName("minutes_per_day").HasColumnType("int").IsRequired();
        builder.Property(p => p.DaysPerWeek).HasColumnName("days_per_week").HasColumnType("int").IsRequired();
        builder.Property(p => p.Pace).HasColumnName("pace").HasColumnType("varchar(32)").IsRequired();
        builder.Property(p => p.PreferredMode).HasColumnName("preferred_mode").HasColumnType("varchar(32)").IsRequired();
        builder.Property(p => p.Note).HasColumnName("note").HasColumnType("varchar(1000)");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(p => p.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => new { p.CenterId, p.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_learning_path_preferences_students");

        builder.HasOne(p => p.Subject)
            .WithMany()
            .HasForeignKey(p => new { p.CenterId, p.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_learning_path_preferences_subjects");
    }
}
