using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed record LearningGoalForecastResult(decimal? Score, string Confidence, string Message);

/// <summary>A shared, explicitly preliminary observation-based estimate, not a calibrated examination score or a direct Twin mastery write.</summary>
public static class LearningGoalForecast
{
    public const string Version = "weighted-observations-1";

    public static async Task<LearningGoalForecastResult> ReadAsync(EduTwinDbContext db, Guid center, Guid student, Guid subject,
        Guid? classId, DateTime now, IReadOnlyCollection<string> targets, CancellationToken ct)
    {
        var scope = await Organization.StudentLearningScope.ResolveAsync(db, center, student, subject, classId, false, ct);
        if (!scope.Allowed) return new(null, "Insufficient", "Chưa xác minh được phạm vi học để dự báo điểm.");
        var nodes = await LearningPathV4Data.NodesAsync(db, center, student, subject, classId, ct);
        if (targets.Count > 0) nodes = nodes.Where(n => targets.Contains(n.NodeId.ToString())).ToList();
        var ids = nodes.Select(n => n.NodeId).ToList();
        var since = now.AddDays(-30);
        var attempts = await db.Attempts.AsNoTracking().Include(a => a.Question).Where(a => a.CenterId == center && a.StudentId == student &&
            a.Question.SubjectId == subject && ids.Contains(a.Question.PrimaryTopicNodeId) && !a.IsPostFeedback && a.SolutionExposedAt == null &&
            !a.Skipped && a.Status == AttemptStatus.Completed && a.CreatedAt >= since).ToListAsync(ct);
        var attemptIds = attempts.Select(a => a.AttemptId).ToList();
        var evidence = await db.EvidenceAssessments.AsNoTracking().Where(e => e.CenterId == center && attemptIds.Contains(e.AttemptId)).ToListAsync(ct);
        var latest = evidence.GroupBy(e => e.AttemptId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.EvaluatedAt).ThenByDescending(e => e.EvidenceAssessmentId).First());
        var corrections = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == center && attemptIds.Contains(a.AttemptId)).ToDictionaryAsync(a => a.AttemptId, ct);
        var scored = new List<(decimal Score, decimal Weight, int Count)>();
        foreach (var node in nodes)
        {
            var sample = attempts.Where(a => a.Question.PrimaryTopicNodeId == node.NodeId && scope.IncludesQuestion(a.Question.PrimaryTopicNodeId, a.Question.GradeLevel) &&
                latest.TryGetValue(a.AttemptId, out var item) && !item.RequiresTeacherReview && item.TrustLevel != EvidenceTrustLevel.ReviewOnly)
                .GroupBy(a => a.QuestionId).Select(g => g.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).First())
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).Take(5).ToList();
            if (sample.Count < 3) continue;
            var correct = sample.Count(a => corrections.GetValueOrDefault(a.AttemptId)?.OverrideIsCorrect ?? a.IsCorrect ?? false);
            scored.Add((10m * correct / sample.Count, node.ExamImportance > 0 ? node.ExamImportance : 1m, sample.Count));
        }
        if (nodes.Count == 0 || scored.Count != nodes.Count)
            return new(null, "Insufficient", $"Chưa đủ dữ liệu: {scored.Count}/{nodes.Count} kỹ năng có ít nhất 3 câu độc lập hợp lệ. Không coi phần chưa biết là 0 điểm.");
        var confidence = scored.Any(s => s.Count < 5) ? "Low" : "Standard";
        var value = Math.Round(scored.Sum(s => s.Score * s.Weight) / scored.Sum(s => s.Weight), 1);
        return new(value, confidence, $"Ước lượng sơ bộ từ câu độc lập gần đây và trọng số nội dung mặc định ({Version}); chưa phải dự báo đã hiệu chuẩn theo đề thi."
            + (confidence == "Low" ? " Mẫu ít, độ tin cậy thấp." : ""));
    }
}
