using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Assignments;

namespace EduTwin.DAL.Persistence.Configurations.Assignments;

public class AssignmentTargetConfiguration : IEntityTypeConfiguration<AssignmentTarget>
{
    public void Configure(EntityTypeBuilder<AssignmentTarget> builder)
    {
        builder.ToTable("assignment_targets");

        // Primary Key
        builder.HasKey(at => new { at.CenterId, at.AssignmentId, at.StudentId })
            .HasName("pk_assignment_targets");
        
        // Properties
        builder.Property(at => at.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)");

        builder.Property(at => at.AssignmentId)
            .HasColumnName("assignment_id")
            .HasColumnType("varchar(36)");

        builder.Property(at => at.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("varchar(36)");

        builder.Property(at => at.TargetSource)
            .HasColumnName("target_source")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(at => at.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(at => at.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(36)")
            .IsRequired();

        // Explicit FK Indexes
        builder.HasIndex(at => new { at.CenterId, at.StudentId })
            .HasDatabaseName("ix_assignment_targets_center_id_student_id");

        // Constraints
        builder.ToTable(t => 
        {
            t.HasCheckConstraint("ck_assignment_targets_target_source", "target_source IN ('WholeClass', 'SelectedStudents', 'GapGroup')");
        });

        // Relations
        builder.HasOne(at => at.Assignment)
            .WithMany()
            .HasForeignKey(at => new { at.CenterId, at.AssignmentId })
            .HasPrincipalKey(a => new { a.CenterId, a.AssignmentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_assignment_targets_assignments_assignment");

        builder.HasOne(at => at.Student)
            .WithMany()
            .HasForeignKey(at => new { at.CenterId, at.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_assignment_targets_students_student");
    }
}
