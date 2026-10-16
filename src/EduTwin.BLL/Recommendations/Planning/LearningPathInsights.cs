using EduTwin.Contracts.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>Observed answers are distinct from independent evidence or a diagnosed misconception.</summary>
public static class LearningPathInsights
{
    public static async Task<LearningInsightsDto> ReadAsync(EduTwinDbContext db, Guid center, Guid student,
        Guid subject, Guid? classId, DateTime now, CancellationToken ct)
    {
        var nodes = await LearningPathV4Data.NodesAsync(db, center, student, subject, classId, ct);
        var ids = nodes.Select(n => n.NodeId).ToList();
        var since = now.AddDays(-30);
        var attempts = await db.Attempts.AsNoTracking().Include(a => a.Question).Where(a => a.CenterId == center &&
            a.StudentId == student && a.Question.SubjectId == subject && ids.Contains(a.Question.PrimaryTopicNodeId) &&
            !a.Skipped && a.CreatedAt >= since).ToListAsync(ct);
        var attemptIds = attempts.Select(a => a.AttemptId).ToList();
        var analyses = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == center && attemptIds.Contains(a.AttemptId)).ToListAsync(ct);
        var analysis = analyses.ToDictionary(a => a.AttemptId);
        // Latest answer to each distinct question prevents repeated failures inflating the weakness count.
        var latest = attempts.GroupBy(a => a.QuestionId).Select(g => g.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).First()).ToList();
        bool? Correct(DAL.AssessmentAndReasoning.Attempt a) => analysis.GetValueOrDefault(a.AttemptId)?.OverrideIsCorrect ?? a.IsCorrect;
        var graded = latest.Where(a => Correct(a).HasValue).ToList();
        var result = new LearningInsightsDto { AnswerCount = (uint)attempts.Count,
            AssignmentCount = (uint)attempts.Where(a => a.AssignmentId.HasValue).Select(a => a.AssignmentId).Distinct().Count(),
            GradedQuestionCount = (uint)graded.Count, PendingCount = (uint)attempts.Count(a => a.Status is AttemptStatus.PendingAnalysis or AttemptStatus.Processing) };
        var scored = latest.Where(a => (analysis.GetValueOrDefault(a.AttemptId)?.OverrideAwardedScore ?? a.AwardedScore).HasValue && a.Question.MaxScore > 0).ToList();
        if (scored.Count > 0) result.ScorePercentage = Math.Round(100m * scored.Sum(a => analysis.GetValueOrDefault(a.AttemptId)?.OverrideAwardedScore ?? a.AwardedScore ?? 0) / scored.Sum(a => a.Question.MaxScore), 1);
        foreach (var node in nodes)
        {
            var answers = latest.Where(a => a.Question.PrimaryTopicNodeId == node.NodeId).ToList();
            var known = answers.Where(a => Correct(a).HasValue).ToList();
            var wrong = known.Count(a => Correct(a) == false);
            var insight = new LearningSkillInsightDto { SkillId = node.NodeId.ToString(), SkillName = node.NodeName,
                QuestionCount = (uint)known.Count, WrongCount = (uint)wrong, CorrectCount = (uint)(known.Count - wrong),
                Observation = known.Count == 0 ? "Chưa đủ kết quả để chọn phần cần củng cố."
                    : $"Câu trả lời gần nhất: đúng {known.Count - wrong}/{known.Count} câu khác nhau; {wrong} câu cần xem lại. Đây chưa phải kết luận thành thạo." };
            insight.ConfirmedMisconceptions = answers.Select(a => analysis.GetValueOrDefault(a.AttemptId))
                .Where(a => a != null && a.ReviewDecision != null && !a.IsFallback && !string.IsNullOrWhiteSpace(a.Misconception))
                .Select(a => a!.Misconception!).Distinct().Take(2).ToList();
            insight.RecentAnswers = answers.OrderByDescending(a => a.CreatedAt).Take(5).Select(a => new LearningAnswerReferenceDto {
                AttemptId = a.AttemptId.ToString(), QuestionText = a.Question.QuestionText, IsCorrect = Correct(a), SubmittedAt = a.CreatedAt,
                Guided = a.IsPostFeedback || a.SolutionExposedAt.HasValue }).ToList();
            result.Skills.Add(insight);
        }
        result.SuggestedSkillIds = result.Skills.Where(s => s.WrongCount > 0).OrderByDescending(s => s.WrongCount / (decimal)s.QuestionCount)
            .ThenByDescending(s => s.WrongCount).Take(3).Select(s => s.SkillId).ToList();
        if (result.SuggestedSkillIds.Count > 0)
            result.Summary = "Nên ưu tiên " + string.Join(", ", result.Skills.Where(s => result.SuggestedSkillIds.Contains(s.SkillId)).Select(s => s.SkillName)) + ". Đề xuất dựa trên câu đã làm; bạn có thể điều chỉnh trước khi chọn tuyến.";
        else if (attempts.Count > 0) result.Summary = "Chưa thấy phần sai rõ ràng từ các câu đã chấm. Hãy kiểm tra phần chưa có bằng chứng hoặc chọn mục tiêu tiếp theo; không cần học lại toàn bộ.";
        return result;
    }

    public static List<DAL.CurriculumAndQuestions.Question> PracticePool(List<DAL.CurriculumAndQuestions.Question> pool, HashSet<ulong> used)
    {
        var fresh = pool.Where(q => !used.Contains(q.QuestionId)).ToList();
        // Reserve confirmation only when there are enough items for a useful practice set.
        // A small bank must still support learning; its independent check will report the shortage.
        var eligible = pool.Count >= 10 ? fresh.Take(Math.Max(0, fresh.Count - 5)) : fresh;
        return pool.Where(q => used.Contains(q.QuestionId)).Concat(eligible).DistinctBy(q => q.QuestionId).ToList();
    }
}
