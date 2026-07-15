using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EduTwin.DAL.Assignments;

namespace EduTwin.DAL.Persistence.Configurations.Assignments;

public class AssignmentQuestionConfiguration : IEntityTypeConfiguration<AssignmentQuestion>
{
    public void Configure(EntityTypeBuilder<AssignmentQuestion> builder)
    {
        builder.ToTable("assignment_questions");

        // Primary Key
        builder.HasKey(aq => new { aq.CenterId, aq.AssignmentId, aq.QuestionId })
            .HasName("pk_assignment_questions");
        
        // Properties
        builder.Property(aq => aq.CenterId)
            .HasColumnName("center_id")
            .HasColumnType("varchar(36)");

        builder.Property(aq => aq.AssignmentId)
            .HasColumnName("assignment_id")
            .HasColumnType("varchar(36)");

        builder.Property(aq => aq.QuestionId)
            .HasColumnName("question_id")
            .HasColumnType("bigint unsigned");

        builder.Property(aq => aq.OrderIndex)
            .HasColumnName("order_index")
            .HasColumnType("int unsigned");

        builder.Property(aq => aq.Points)
            .HasColumnName("points")
            .HasColumnType("decimal(5,2)");

        builder.Property(aq => aq.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        // Indexes
        builder.HasIndex(aq => new { aq.CenterId, aq.AssignmentId, aq.OrderIndex })
            .IsUnique()
            .HasDatabaseName("ux_assignment_questions_center_id_assignment_id_order_index");

        // Explicit FK Indexes
        builder.HasIndex(aq => new { aq.CenterId, aq.QuestionId })
            .HasDatabaseName("ix_assignment_questions_center_id_question_id");

        // Constraints
        builder.ToTable(t => 
        {
            t.HasCheckConstraint("ck_assignment_questions_points", "points > 0");
        });

        // Relations
        builder.HasOne(aq => aq.Assignment)
            .WithMany()
            .HasForeignKey(aq => new { aq.CenterId, aq.AssignmentId })
            .HasPrincipalKey(a => new { a.CenterId, a.AssignmentId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_assignment_questions_assignments_assignment");

        builder.HasOne(aq => aq.Question)
            .WithMany()
            .HasForeignKey(aq => new { aq.CenterId, aq.QuestionId })
            .HasPrincipalKey(q => new { q.CenterId, q.QuestionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_assignment_questions_questions_question");
    }
}
