using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.Assignments;

public static class AssignmentFinalReviewPolicy
{
    public static AssignmentFinalReviewEligibilityDto Evaluate(
        IReadOnlyCollection<AssignmentQuestion> questions, IEnumerable<Attempt> attempts,
        ISet<ulong>? rubricQuestionIds = null, ISet<ulong>? rubricGradedAttemptIds = null)
    {
        var latest = attempts.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId)
            .GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.First());
        var result = new AssignmentFinalReviewEligibilityDto();
        foreach (var question in questions.Where(q => !q.IsVoided))
        {
            if (!latest.TryGetValue(question.QuestionId, out var attempt)) result.MissingQuestionCount++;
            else if (attempt.Status == AttemptStatus.NeedsTeacherReview) result.PendingReviewQuestionCount++;
            else if (attempt.Status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing) result.ProcessingQuestionCount++;
            else if (attempt.Status != AttemptStatus.Completed) result.FailedQuestionCount++;
            else if (rubricQuestionIds?.Contains(question.QuestionId) == true &&
                rubricGradedAttemptIds?.Contains(attempt.AttemptId) != true) result.PendingReviewQuestionCount++;
        }
        result.CanApprove = questions.Count > 0 && result.MissingQuestionCount == 0 &&
            result.PendingReviewQuestionCount == 0 && result.ProcessingQuestionCount == 0 && result.FailedQuestionCount == 0;
        result.BlockReason = questions.Count == 0 ? "Bài tập chưa có câu hỏi."
            : result.MissingQuestionCount > 0 ? $"Còn {result.MissingQuestionCount} câu chưa nộp."
            : result.PendingReviewQuestionCount > 0 ? $"Còn {result.PendingReviewQuestionCount} câu cần giáo viên chấm hoặc xác nhận."
            : result.ProcessingQuestionCount > 0 ? $"Còn {result.ProcessingQuestionCount} câu đang được phân tích."
            : result.FailedQuestionCount > 0 ? $"Còn {result.FailedQuestionCount} câu phân tích lỗi, cần xử lý trước khi chốt."
            : null;
        return result;
    }
}
