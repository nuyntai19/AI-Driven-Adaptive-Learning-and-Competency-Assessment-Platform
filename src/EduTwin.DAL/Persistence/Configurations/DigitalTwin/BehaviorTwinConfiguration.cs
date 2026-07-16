using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.DAL.Persistence.Configurations.DigitalTwin;

public class BehaviorTwinConfiguration : IEntityTypeConfiguration<BehaviorTwin>
{
    public void Configure(EntityTypeBuilder<BehaviorTwin> builder)
    {
        builder.ToTable("behavior_twins");

        builder.HasKey(b => b.BehaviorTwinId).HasName("pk_behavior_twins");

        builder.HasIndex(b => new { b.CenterId, b.StudentId, b.SubjectId })
            .IsUnique()
            .HasDatabaseName("ux_behavior_twins_center_id_student_id_subject_id");

        builder.HasIndex(b => new { b.CenterId, b.SubjectId })
            .HasDatabaseName("ix_behavior_twins_center_id_subject_id");

        builder.Property(b => b.BehaviorTwinId).HasColumnName("behavior_twin_id").HasColumnType("bigint unsigned").ValueGeneratedNever();
        builder.Property(b => b.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(b => b.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(b => b.AvgTimeSpentSeconds).HasColumnName("avg_time_spent_seconds").HasColumnType("decimal(10,2)").HasDefaultValue(0m);
        builder.Property(b => b.SkipRate).HasColumnName("skip_rate").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(b => b.ChangeAnswerRate).HasColumnName("change_answer_rate").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(b => b.AvgConfidence).HasColumnName("avg_confidence").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(b => b.ConfidenceCalibration).HasColumnName("confidence_calibration").HasColumnType("decimal(5,2)").HasDefaultValue(0m);
        builder.Property(b => b.AttemptCount).HasColumnName("attempt_count").HasColumnType("int unsigned").HasDefaultValue(0u);

        builder.Property(b => b.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(b => b.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(b => b.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(b => b.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(b => b.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(b => b.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_behavior_twins_skip_rate", "`skip_rate` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_behavior_twins_change_answer_rate", "`change_answer_rate` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_behavior_twins_avg_confidence", "`avg_confidence` BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_behavior_twins_confidence_calibration", "`confidence_calibration` BETWEEN 0 AND 100");
        });

        builder.HasOne(b => b.Student)
            .WithMany()
            .HasForeignKey(b => new { b.CenterId, b.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_behavior_twins_students_student");

        builder.HasOne(b => b.Subject)
            .WithMany()
            .HasForeignKey(b => new { b.CenterId, b.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_behavior_twins_subjects_subject");
    }
}
