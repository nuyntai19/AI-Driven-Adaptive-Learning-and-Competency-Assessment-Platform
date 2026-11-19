using EduTwin.Contracts.Assignments;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

internal static class ExamResultVisibility
{
    public static bool IsTermExam(AssignmentKind kind) => kind is AssignmentKind.Midterm or AssignmentKind.Final;

    public static IQueryable<Attempt> WithVisibleResults(EduTwinDbContext db, IQueryable<Attempt> attempts) =>
        attempts.Where(attempt => !db.Assignments.Any(a => a.CenterId == attempt.CenterId &&
            a.AssignmentId == attempt.AssignmentId && (a.Kind == AssignmentKind.Midterm || a.Kind == AssignmentKind.Final) &&
            (a.ResultsPublishedAt == null || !db.StudentAssignmentProgresses.Any(p =>
                p.CenterId == attempt.CenterId && p.AssignmentId == a.AssignmentId && p.StudentId == attempt.StudentId &&
                !p.IsDeleted && p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved))));

    public static Task<bool> IsEmbargoedAsync(EduTwinDbContext db, Guid center, Guid assignment, Guid student, CancellationToken ct) =>
        db.Assignments.AsNoTracking().AnyAsync(a => a.CenterId == center && a.AssignmentId == assignment &&
            (a.Kind == AssignmentKind.Midterm || a.Kind == AssignmentKind.Final) &&
            (a.ResultsPublishedAt == null || !db.StudentAssignmentProgresses.Any(p =>
                p.CenterId == center && p.AssignmentId == assignment && p.StudentId == student &&
                p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved && !p.IsDeleted)), ct);
}
