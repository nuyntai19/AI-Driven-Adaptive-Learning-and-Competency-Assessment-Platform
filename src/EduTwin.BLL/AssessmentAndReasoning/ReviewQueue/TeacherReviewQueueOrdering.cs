using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public static class TeacherReviewQueueOrdering
{
    public static IOrderedQueryable<EvidenceAssessment> Apply(
        IQueryable<EvidenceAssessment> items, EduTwinDbContext db, Guid centerId, bool studentDetail) =>
        studentDetail
            ? items.OrderBy(evidence => db.AssignmentQuestions
                .Where(aq => aq.CenterId == centerId && aq.AssignmentId == evidence.Attempt.AssignmentId &&
                             aq.QuestionId == evidence.Attempt.QuestionId)
                .Select(aq => (uint?)aq.OrderIndex).FirstOrDefault() ?? uint.MaxValue)
                .ThenBy(evidence => evidence.AttemptId)
            : items.OrderBy(evidence => evidence.EvaluatedAt)
                .ThenBy(evidence => evidence.EvidenceAssessmentId);
}
