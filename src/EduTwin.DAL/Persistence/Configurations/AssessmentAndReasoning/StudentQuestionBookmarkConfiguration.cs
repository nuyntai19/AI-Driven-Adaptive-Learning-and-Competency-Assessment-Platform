using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EduTwin.DAL.Persistence.Configurations.AssessmentAndReasoning;

public sealed class StudentQuestionBookmarkConfiguration : IEntityTypeConfiguration<StudentQuestionBookmark>
{
    public void Configure(EntityTypeBuilder<StudentQuestionBookmark> b)
    {
        b.ToTable("student_question_bookmarks");
        b.HasKey(x => new { x.CenterId, x.StudentId, x.QuestionId });
        b.Property(x => x.CenterId).HasColumnName("center_id").HasColumnType("varchar(36)");
        b.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("varchar(36)");
        b.Property(x => x.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("varchar(36)");
        b.HasIndex(x => new { x.CenterId, x.QuestionId }).HasDatabaseName("ix_student_question_bookmarks_question");
        b.HasOne<Question>().WithMany().HasForeignKey(x => new { x.CenterId, x.QuestionId })
            .HasPrincipalKey(x => new { x.CenterId, x.QuestionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
