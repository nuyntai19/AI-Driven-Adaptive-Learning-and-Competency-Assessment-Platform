using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.CurriculumAndQuestions;

public class ClassCurriculumApplicationConfiguration : IEntityTypeConfiguration<ClassCurriculumApplication>
{
    public void Configure(EntityTypeBuilder<ClassCurriculumApplication> b)
    {
        b.ToTable("class_curriculum_applications", t =>
        {
            t.HasCheckConstraint("ck_class_curriculum_application_role", "application_role IN ('Primary','Supplemental')");
            t.HasCheckConstraint("ck_class_curriculum_application_dates", "(ended_at IS NULL AND ended_by IS NULL) OR (ended_at IS NOT NULL AND ended_at >= started_at AND ended_by IS NOT NULL)");
            t.HasCheckConstraint("ck_class_curriculum_application_grades", "(class_grade_at_start IS NULL OR class_grade_at_start BETWEEN 10 AND 12) AND (curriculum_grade_at_start IS NULL OR curriculum_grade_at_start BETWEEN 10 AND 12)");
            t.HasCheckConstraint("ck_class_curriculum_application_exception", "(is_grade_exception=0 AND grade_mismatch_reason IS NULL AND exception_approved_by IS NULL AND exception_approved_at IS NULL) OR (is_grade_exception=1 AND grade_mismatch_reason IS NOT NULL AND CHAR_LENGTH(TRIM(grade_mismatch_reason))>0 AND exception_approved_by IS NOT NULL AND exception_approved_at IS NOT NULL)");
        });
        b.HasKey(x => x.ApplicationId);
        foreach (var name in new[] { nameof(ClassCurriculumApplication.ApplicationId), nameof(ClassCurriculumApplication.CenterId), nameof(ClassCurriculumApplication.ClassId), nameof(ClassCurriculumApplication.CurriculumId), nameof(ClassCurriculumApplication.SubjectId), nameof(ClassCurriculumApplication.AssignedBy), nameof(ClassCurriculumApplication.EndedBy), nameof(ClassCurriculumApplication.ExceptionApprovedBy) })
            b.Property(name).HasColumnType("varchar(36)");
        b.Property(x => x.ApplicationRole).HasMaxLength(16);
        b.Property(x => x.ChangeReason).HasMaxLength(500);
        b.Property(x => x.EndReason).HasMaxLength(500);
        b.Property(x => x.GradeMismatchReason).HasMaxLength(500);
        b.Property(x => x.ClassGradeAtStart).HasColumnType("tinyint unsigned");
        b.Property(x => x.CurriculumGradeAtStart).HasColumnType("tinyint unsigned");
        b.Property(x => x.CurrentPrimaryKey).HasColumnType("varchar(36)")
            .HasComputedColumnSql("CASE WHEN ended_at IS NULL AND application_role='Primary' THEN class_id ELSE NULL END", stored: true);
        b.Property(x => x.CurrentCurriculumKey).HasColumnType("varchar(36)")
            .HasComputedColumnSql("CASE WHEN ended_at IS NULL THEN curriculum_id ELSE NULL END", stored: true);
        b.HasIndex(x => new { x.CenterId, x.CurrentPrimaryKey }).IsUnique();
        b.HasIndex(x => new { x.CenterId, x.ClassId, x.CurrentCurriculumKey }).IsUnique();
        b.HasIndex(x => new { x.CenterId, x.ClassId, x.StartedAt });
        b.HasOne(x => x.Class).WithMany().HasForeignKey(x => new { x.CenterId, x.ClassId })
            .HasPrincipalKey(x => new { x.CenterId, x.ClassId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Curriculum).WithMany().HasForeignKey(x => new { x.CenterId, x.CurriculumId })
            .HasPrincipalKey(x => new { x.CenterId, x.CurriculumId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EduTwin.DAL.IdentityAndTenancy.User>().WithMany().HasForeignKey(x => new { x.CenterId, x.AssignedBy })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EduTwin.DAL.IdentityAndTenancy.User>().WithMany().HasForeignKey(x => new { x.CenterId, x.ExceptionApprovedBy })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EduTwin.DAL.IdentityAndTenancy.User>().WithMany().HasForeignKey(x => new { x.CenterId, x.EndedBy })
            .HasPrincipalKey(x => new { x.CenterId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in b.Metadata.GetProperties())
        {
            property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)) property.SetColumnType("datetime(6)");
        }
    }
}
