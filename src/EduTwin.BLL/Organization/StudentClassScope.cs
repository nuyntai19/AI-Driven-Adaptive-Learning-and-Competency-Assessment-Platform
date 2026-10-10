using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Organization;

internal sealed class StudentClassMembership
{
    public Guid ClassId { get; set; }
    public Guid SubjectId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public byte? GradeLevel { get; set; }
    public bool IsHistorical { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
}

internal static class StudentClassScope
{
    // Class lifecycle and this student's membership are separate: a student can
    // leave a class that is still active for the remaining students.
    public static IQueryable<StudentClassMembership> Memberships(EduTwinDbContext db, Guid center, Guid student) =>
        db.ClassStudents.Where(m => m.CenterId == center && m.StudentId == student &&
            m.Class != null && !m.Class.IsDeleted)
        .Select(m => new StudentClassMembership
        {
            ClassId = m.ClassId, SubjectId = m.Class.SubjectId, ClassName = m.Class.ClassName,
            GradeLevel = m.Class.GradeLevel, JoinedAt = m.JoinedAt, RemovedAt = m.RemovedAt,
            IsHistorical = m.Status != ClassStudentStatus.Active || m.Class.Status != ClassStatus.Active ||
                m.Class.LearningScope != ClassLearningScope.Current
        });

    public static IQueryable<StudentClassMembership> InView(EduTwinDbContext db, Guid center, Guid student, bool history) =>
        Memberships(db, center, student).Where(m => m.IsHistorical == history);
}
