using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Services;

public static class LearningPathRunExpiry
{
    // Invoke before acquiring a write transaction. Evaluation owns the student lock and follow-up transaction.
    public static async Task CloseAsync(EduTwinDbContext db, TimeProvider clock, Guid center, Guid student,
        Guid? subject, CancellationToken ct)
    {
        var candidates = await db.LearningPathSessionRuns.AsNoTracking().Where(r => r.CenterId == center &&
            r.StudentId == student && r.Status == SessionRunStatus.InProgress &&
            (!subject.HasValue || r.Session.LearningPath.SubjectId == subject) && r.Session.LearningPath.PlanSchemaVersion == "4.0")
            .Select(r => new { r.SessionRunId, r.StartedAt, r.PolicySnapshot }).ToListAsync(ct);
        var evaluator = new LearningPathV4Evaluator(db, clock);
        foreach (var run in candidates.Where(r => LearningAssessmentPolicy.ExpiresAt(r.PolicySnapshot, r.StartedAt) is { } end && end <= clock.GetUtcNow().UtcDateTime))
            await evaluator.EvaluateMissionRunAsync(center, student, run.SessionRunId, ct);
    }
}
