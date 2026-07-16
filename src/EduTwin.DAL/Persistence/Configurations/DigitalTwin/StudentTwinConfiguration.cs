using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.DAL.Persistence.Configurations.DigitalTwin;

public class StudentTwinConfiguration : IEntityTypeConfiguration<StudentTwin>
{
    public void Configure(EntityTypeBuilder<StudentTwin> builder)
    {
        builder.ToTable("student_twins");

        builder.HasKey(t => t.TwinId).HasName("pk_student_twins");

        builder.HasIndex(t => new { t.CenterId, t.StudentId })
            .IsUnique()
            .HasDatabaseName("ux_student_twins_center_id_student_id");

        builder.Property(t => t.TwinId).HasColumnName("twin_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(t => t.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(t => t.OverallMastery).HasColumnName("overall_mastery").HasColumnType("decimal(5,2)");
        builder.Property(t => t.LastEvidenceAt).HasColumnName("last_evidence_at").HasColumnType("datetime(6)");

        builder.Property(t => t.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").HasColumnType("varchar(36)");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasColumnType("tinyint(1)").HasDefaultValue(false);
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime(6)");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by").HasColumnType("varchar(36)");
        builder.Property(t => t.RowVersion).HasColumnName("row_version").HasColumnType("bigint unsigned").HasDefaultValue(1ul).IsConcurrencyToken();

        builder.ToTable(t => t.HasCheckConstraint("ck_student_twins_overall_mastery", "`overall_mastery` BETWEEN 0 AND 100"));

        builder.HasOne(t => t.Student)
            .WithMany()
            .HasForeignKey(t => new { t.CenterId, t.StudentId })
            .HasPrincipalKey(s => new { s.CenterId, s.StudentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_student_twins_students_student");
    }
}
