using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Recommendations;

namespace EduTwin.DAL.Persistence.Configurations.Recommendations;

public class LearningPathConfiguration : IEntityTypeConfiguration<LearningPath>
{
    public void Configure(EntityTypeBuilder<LearningPath> builder)
    {
        builder.ToTable("learning_paths");

        builder.HasKey(l => l.LearningPathId).HasName("pk_learning_paths");

        builder.HasAlternateKey(l => new { l.CenterId, l.LearningPathId })
            .HasName("ux_learning_paths_center_id_learning_path_id");

        builder.HasIndex(l => new { l.CenterId, l.StudentId, l.SubjectId, l.Status })
            .HasDatabaseName("ix_learning_paths_center_id_student_id_subject_id_status");

        builder.HasIndex(l => new { l.CenterId, l.SubjectId })
            .HasDatabaseName("ix_learning_paths_center_id_subject_id");

        builder.HasIndex(l => new { l.CenterId, l.GeneratedFromAttemptId })
            .HasDatabaseName("ix_learning_paths_center_id_generated_from_attempt_id");

        builder.Property(l => l.LearningPathId).HasColumnName("learning_path_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(l => l.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(l => l.SubjectId).HasColumnName("subject_id").HasColumnType("varchar(36)").IsRequired();

        builder.Property(l => l.Strategy)
            .HasColumnName("strategy")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(l => l.Version).HasColumnName("version").HasColumnType("int unsigned");

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(l => l.GeneratedFromAttemptId).HasColumnName("generated_from_attempt_id").HasColumnType("bigint unsigned");
        builder.Property(l => l.GeneratedAt).HasColumnName("generated_at").HasColumnType("datetime(6)");

        builder.Property(l => l.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(l => l.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(l => l.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_learning_paths_strategy", "`strategy` IN ('LinearFallback', 'OpportunityGap')");
            t.HasCheckConstraint("ck_learning_paths_status", "`status` IN ('Active', 'Superseded', 'Completed')");
        });

        builder.HasOne(l => l.Student)
            .WithMany()
            .HasForeignKey(l => new { l.CenterId, l.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_paths_students_student");

        builder.HasOne(l => l.Subject)
            .WithMany()
            .HasForeignKey(l => new { l.CenterId, l.SubjectId })
            .HasPrincipalKey(s => new { s.CenterId, s.SubjectId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_paths_subjects_subject");

        builder.HasOne(l => l.GeneratedFromAttempt)
            .WithMany()
            .HasForeignKey(l => new { l.CenterId, l.GeneratedFromAttemptId })
            .HasPrincipalKey(a => new { a.CenterId, a.AttemptId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_learning_paths_attempts_generated_from_attempt");
    }
}
