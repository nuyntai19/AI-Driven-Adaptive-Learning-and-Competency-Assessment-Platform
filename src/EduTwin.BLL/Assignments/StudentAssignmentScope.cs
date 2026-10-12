using EduTwin.Contracts.Organization;
using EduTwin.BLL.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

public static class StudentAssignmentScope
{
    public static Task<bool> ValidCurrentClassAsync(EduTwinDbContext db, Guid center, Guid student, Guid classId, Guid? subject, CancellationToken ct) =>
        ValidClassAsync(db, center, student, classId, subject, false, ct);

    public static Task<bool> ValidClassAsync(EduTwinDbContext db, Guid center, Guid student, Guid classId, Guid? subject, bool history, CancellationToken ct) =>
        StudentClassScope.InView(db, center, student, history).AnyAsync(m => m.ClassId == classId &&
            (!subject.HasValue || subject == Guid.Empty || m.SubjectId == subject), ct);

    // The assignment-class FK enforces existence. This guard rejects suspended parent classes.
    public static Task<bool> SuspendedAsync(EduTwinDbContext db, Guid center, Guid classId, CancellationToken ct) =>
        db.Classes.AnyAsync(c => c.CenterId == center && c.ClassId == classId &&
            (c.IsDeleted || c.Status != ClassStatus.Active || c.LearningScope != ClassLearningScope.Current), ct);

    public static IQueryable<StudentAssignmentProgress> Current(IQueryable<StudentAssignmentProgress> query,
        EduTwinDbContext db, Guid center, Guid student) => InView(query, db, center, student, false);

    public static IQueryable<StudentAssignmentProgress> InView(IQueryable<StudentAssignmentProgress> query,
        EduTwinDbContext db, Guid center, Guid student, bool history) => query.Where(p =>
            StudentClassScope.InView(db, center, student, history).Any(m => m.ClassId == p.Assignment!.ClassId));
}
