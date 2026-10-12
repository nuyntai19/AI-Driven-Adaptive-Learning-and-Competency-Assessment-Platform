using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations;

public class TeacherStudentNoteConfiguration : IEntityTypeConfiguration<Organization.TeacherStudentNote>
{
    public void Configure(EntityTypeBuilder<Organization.TeacherStudentNote> builder)
    {
        builder.ToTable("teacher_student_notes");

        builder.HasKey(n => n.NoteId)
            .HasName("pk_teacher_student_notes");

        builder.Property(n => n.NoteId)
            .HasColumnName("note_id")
            .HasColumnType("VARCHAR(36)");

        builder.Property(n => n.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(n => n.TeacherId)
            .HasColumnName("teacher_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(n => n.StudentId)
            .HasColumnName("student_id")
            .HasColumnType("VARCHAR(36)")
            .IsRequired();

        builder.Property(n => n.Content)
            .HasColumnName("content")
            .HasColumnType("TEXT")
            .IsRequired();

        // IMutableTenantAggregate fields
        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(n => n.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(n => n.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("DATETIME(6)")
            .IsRequired();

        builder.Property(n => n.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(n => n.IsDeleted)
            .HasColumnName("is_deleted")
            .HasColumnType("TINYINT(1)")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(n => n.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("DATETIME(6)");

        builder.Property(n => n.DeletedBy)
            .HasColumnName("deleted_by")
            .HasColumnType("VARCHAR(36)");

        builder.Property(n => n.RowVersion)
            .HasColumnName("row_version")
            .HasColumnType("BIGINT UNSIGNED")
            .HasDefaultValue(1ul)
            .IsConcurrencyToken()
            .IsRequired();

        // Alternate Key
        builder.HasAlternateKey(n => new { n.CenterId, n.NoteId })
            .HasName("ux_teacher_student_notes_center_id_note_id");

        // Indexes
        builder.HasIndex(n => new { n.CenterId, n.TeacherId, n.StudentId })
            .HasDatabaseName("ix_teacher_student_notes_center_id_teacher_id_student_id");

        builder.HasIndex(n => new { n.CenterId, n.StudentId })
            .HasDatabaseName("ix_teacher_student_notes_center_id_student_id");

        // Relations
        builder.HasOne(n => n.Teacher)
            .WithMany()
            .HasForeignKey(n => new { n.CenterId, n.TeacherId })
            .HasPrincipalKey(t => new { t.CenterId, t.TeacherId })
            .HasConstraintName("fk_teacher_student_notes_teachers")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Student)
            .WithMany()
            .HasForeignKey(n => new { n.CenterId, n.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .HasConstraintName("fk_teacher_student_notes_students")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
