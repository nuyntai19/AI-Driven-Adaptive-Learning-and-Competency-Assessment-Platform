using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Assignments;

namespace EduTwin.DAL.Persistence.Configurations.Assignments;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments");

        // Primary Key
        builder.HasKey(a => a.AssignmentId).HasName("pk_assignments");

        // Alternate Key
        builder.HasAlternateKey(a => new { a.CenterId, a.AssignmentId })
            .HasName("ux_assignments_center_id_assignment_id");

        // Properties
        builder.Property(a => a.AssignmentId)
            .HasColumnName("assignment_id")
            .HasColumnType("varchar(36)");

        builder.Property(a => a.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(a => a.ClassId)
            .HasColumnName("class_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(a => a.CreatedByTeacherId)
            .HasColumnName("created_by_teacher_id")
            .HasColumnType("varchar(36)")
            .IsRequired();

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasColumnType("varchar(250)")
            .IsRequired();

        builder.Property(a => a.Instructions)
            .HasColumnName("instructions")
            .HasColumnType("text");

        builder.Property(a => a.DueAt)
            .HasColumnName("due_at")
            .HasColumnType("datetime(6)");

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(32)")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("datetime(6)");

        // MTA Properties
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(a => a.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        // Indexes
        builder.HasIndex(a => new { a.CenterId, a.ClassId, a.Status, a.DueAt })
            .HasDatabaseName("ix_assignments_center_id_class_id_status_due_at");

        // Explicit FK Indexes
        builder.HasIndex(a => new { a.CenterId, a.CreatedByTeacherId })
            .HasDatabaseName("ix_assignments_center_id_created_by_teacher_id");

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_assignments_status", "status IN ('Draft', 'Published', 'Closed', 'Archived')");
        });

        // Relations
        builder.HasOne(a => a.Class)
            .WithMany()
            .HasForeignKey(a => new { a.CenterId, a.ClassId })
            .HasPrincipalKey(c => new { c.CenterId, c.ClassId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_assignments_classes_class");

        builder.HasOne(a => a.CreatedByTeacher)
            .WithMany()
            .HasForeignKey(a => new { a.CenterId, a.CreatedByTeacherId })
            .HasPrincipalKey(t => new { t.CenterId, t.TeacherId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_assignments_teachers_created_by_teacher");
    }
}
