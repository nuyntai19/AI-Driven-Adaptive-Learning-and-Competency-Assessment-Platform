using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public static class LearningPathPlacementBaseline
{
    // Baseline affects the amount/order of learning, never grants independent mastery by itself.
    public static async Task<Dictionary<string, string>> ReadAsync(EduTwinDbContext db, Guid center, Guid student,
        Guid subject, Guid? classId, CancellationToken ct)
    {
        var runs = await db.LearningPathSessionRuns.AsNoTracking().Where(r => r.CenterId == center && r.StudentId == student &&
            r.Session.LearningPath.SubjectId == subject && r.Session.ClassId == classId && r.Status == SessionRunStatus.Completed &&
            r.Session.LearningPath.PlanSchemaVersion == "4.0").OrderByDescending(r => r.CompletedAt)
            .Select(r => new { r.SessionRunId, r.PolicySnapshot, r.Session.TopicNodeId }).ToListAsync(ct);
        var baselines = runs.Where(r => r.PolicySnapshot?.RootElement.TryGetProperty("isPlacement", out var flag) == true && flag.ValueKind == System.Text.Json.JsonValueKind.True)
            .GroupBy(r => r.TopicNodeId).Select(g => g.First()).ToList();
        var ids = baselines.Select(r => r.SessionRunId).ToList();
        var answers = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == center && ids.Contains(i.ActivityRun.SessionRunId) &&
            i.Attempt != null && i.ExposureMode == "Independent" && i.SolutionExposedAt == null)
            .Select(i => new { i.ActivityRun.SessionRunId, i.QuestionId, i.Attempt!.AttemptId, i.Attempt.IsCorrect, i.Attempt.Skipped, i.Attempt.Question.PrimaryTopicNodeId }).ToListAsync(ct);
        var attempts = answers.Select(a => a.AttemptId).ToList();
        var reviews = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == center && attempts.Contains(a.AttemptId)).ToDictionaryAsync(a => a.AttemptId, ct);
        return baselines.ToDictionary(r => r.TopicNodeId.ToString(), r => {
            if (answers.Any(a => a.SessionRunId == r.SessionRunId && a.PrimaryTopicNodeId == r.TopicNodeId && a.Skipped)) return "Inconclusive";
            var valid = answers.Where(a => a.SessionRunId == r.SessionRunId && a.PrimaryTopicNodeId == r.TopicNodeId)
                .DistinctBy(a => a.QuestionId).Select(a => reviews.GetValueOrDefault(a.AttemptId)?.OverrideIsCorrect ?? a.IsCorrect).Where(a => a.HasValue).ToList();
            return LearningAssessmentPolicy.ClassifyPlacement(valid.Count, valid.Count(a => a == true));
        });
    }
}
