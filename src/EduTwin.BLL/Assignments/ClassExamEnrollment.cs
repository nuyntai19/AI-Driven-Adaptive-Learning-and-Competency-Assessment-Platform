using System.Security.Cryptography;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

public static class ClassExamEnrollment
{
    // Stage in the enrollment unit of work, retaining the same assignment and submission pipeline.
    public static async Task StageAsync(EduTwinDbContext db, Guid center, Guid classId,
        IReadOnlyCollection<Guid> students, DateTime now, CancellationToken ct, Guid? actor = null)
    {
        var exams = await db.Assignments.AsNoTracking().Where(a => a.CenterId == center && a.ClassId == classId &&
            a.Kind != AssignmentKind.Regular && a.Status == AssignmentStatus.Published && a.TargetMode == TargetSource.WholeClass &&
            a.ExamAt > now).ToListAsync(ct);
        foreach (var exam in exams)
        {
            var targets = await db.AssignmentTargets.Where(t => t.CenterId == center && t.AssignmentId == exam.AssignmentId).Select(t => t.StudentId).ToListAsync(ct);
            var progresses = await db.StudentAssignmentProgresses.Where(p => p.CenterId == center && p.AssignmentId == exam.AssignmentId).Select(p => p.StudentId).ToListAsync(ct);
            var count = (uint)await db.AssignmentQuestions.CountAsync(q => q.CenterId == center && q.AssignmentId == exam.AssignmentId && !q.IsVoided, ct);
            foreach (var student in students.Distinct())
            {
                if (!targets.Contains(student)) db.AssignmentTargets.Add(new AssignmentTarget {
                    CenterId = center, AssignmentId = exam.AssignmentId, StudentId = student,
                    TargetSource = TargetSource.WholeClass, CreatedAt = now, CreatedBy = actor ?? exam.CreatedByTeacherId });
                if (!progresses.Contains(student)) db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress {
                    ProgressId = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8)) | 1UL,
                    CenterId = center, AssignmentId = exam.AssignmentId, StudentId = student, Status = ProgressStatus.NotStarted,
                    TotalQuestionCount = count, CreatedAt = now, UpdatedAt = now, CreatedBy = actor, UpdatedBy = actor, RowVersion = 1 });
            }
        }
    }
}
