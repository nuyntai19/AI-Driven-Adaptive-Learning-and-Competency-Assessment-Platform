using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

public sealed record AssignmentRubricReviewState(HashSet<ulong> QuestionIds, HashSet<ulong> GradedAttemptIds)
{
    public static async Task<AssignmentRubricReviewState> LoadAsync(EduTwinDbContext db, Guid centerId,
        Guid assignmentId, Guid? studentId, CancellationToken cancellationToken)
    {
        var definitions = await db.Questions.AsNoTracking()
            .Where(q => q.CenterId == centerId && db.AssignmentQuestions.Any(aq =>
                aq.CenterId == centerId && aq.AssignmentId == assignmentId && aq.QuestionId == q.QuestionId && !aq.IsVoided))
            .Select(q => new { q.QuestionId, q.GradingCriteria }).ToListAsync(cancellationToken);
        var questionIds = definitions.Where(q => q.GradingCriteria?.Criteria.Count > 0).Select(q => q.QuestionId).ToHashSet();
        if (questionIds.Count == 0) return new(questionIds, []);

        // Only the current review version counts. An earlier rubric must never
        // permit final approval of a different submission or a later assessment.
        var histories = db.TeacherReviewHistories.AsNoTracking().Where(h => h.CenterId == centerId &&
            h.Attempt.AssignmentId == assignmentId && h.RubricResultJson != null &&
            h.OverrideVersion == h.Analysis.OverrideVersion);
        if (studentId.HasValue) histories = histories.Where(h => h.Attempt.StudentId == studentId.Value);
        return new(questionIds, (await histories.Select(h => h.AttemptId).ToListAsync(cancellationToken)).ToHashSet());
    }
}
