using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed partial class LearningPathV4Planner
{
    // Report only the latest non-abandoned run of each placement section, never unrelated practice scores.
    private async Task<PlacementReportDto> PlacementReportAsync(Guid center, Guid student, LearningPath path,
        PathProposalDto plan, CapabilityMapDto map, List<LearningPathSessionRun> runs, List<AgendaItemDto> missions, CancellationToken ct)
    {
        var report = new PlacementReportDto();
        var latest = runs.Where(r => r.Status != SessionRunStatus.Abandoned).GroupBy(r => r.SessionId)
            .Select(g => g.OrderByDescending(r => r.RunNumber).First()).ToList();
        var runIds = latest.Select(r => r.SessionRunId).ToList();
        var issued = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == center &&
            i.ActivityRun.SessionRun.StudentId == student && i.ActivityRun.SessionRun.Session.LearningPathId == path.LearningPathId &&
            runIds.Contains(i.ActivityRun.SessionRunId) && i.AttemptId != null)
            .Select(i => new { i.ActivityRun.SessionRunId, i.AttemptId, i.Attempt!.IsCorrect, i.Attempt.Skipped,
                i.Attempt.AwardedScore, i.Attempt.Question.MaxScore, i.Attempt.Question.PrimaryTopicNodeId, i.Attempt.Question.QuestionType }).ToListAsync(ct);
        var attemptIds = issued.Select(i => i.AttemptId!.Value).ToList();
        var reviews = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == center && attemptIds.Contains(a.AttemptId))
            .ToDictionaryAsync(a => a.AttemptId, ct);
        foreach (var milestone in plan.Milestones)
        foreach (var id in milestone.SkillIds)
        {
            var skill = map.Skills.FirstOrDefault(s => s.SkillId == id);
            if (skill == null) continue;
            var missionIds = milestone.Missions.Select(m => m.MissionId).ToHashSet();
            var run = latest.FirstOrDefault(r => missionIds.Contains(r.SessionId.ToString()));
            var answers = issued.Where(i => i.SessionRunId == run?.SessionRunId && i.PrimaryTopicNodeId.ToString() == id && !i.Skipped)
                .Select(i => new { Correct = reviews.GetValueOrDefault(i.AttemptId!.Value)?.OverrideIsCorrect ?? i.IsCorrect,
                    Earned = reviews.GetValueOrDefault(i.AttemptId!.Value)?.OverrideAwardedScore ?? i.AwardedScore ??
                        ((reviews.GetValueOrDefault(i.AttemptId.Value)?.OverrideIsCorrect ?? i.IsCorrect) == true ? i.MaxScore : 0), i.MaxScore, i.QuestionType })
                .Where(a => a.Correct.HasValue).ToList();
            var correct = answers.Count(a => a.Correct == true);
            var possible = answers.Sum(a => a.MaxScore);
            var earned = answers.Sum(a => Math.Clamp(a.Earned, 0, a.MaxScore));
            var assessed = run?.Status == SessionRunStatus.Completed;
            var confirmed = skill.State == "Confirmed";
            var result = new PlacementSkillResultDto
            {
                Classification = !assessed || issued.Any(i => i.SessionRunId == run?.SessionRunId && i.PrimaryTopicNodeId.ToString() == id && i.Skipped)
                    ? "Inconclusive" : LearningAssessmentPolicy.ClassifyPlacement(answers.Count, correct),
                Confidence = !assessed || answers.Count < 3 ? "Insufficient" : answers.Count < 5 ? "Low" : "Standard",
                SkillId = id, SkillName = skill.SkillName, TopicName = skill.TopicName,
                AnsweredQuestions = (uint)answers.Count, CorrectAnswers = (uint)correct,
                EarnedScore = assessed ? earned : 0, PossibleScore = assessed ? possible : 0,
                ScoreOutOfTen = assessed && possible > 0 ? Math.Round(10m * earned / possible, 1) : null,
                // Earlier evidence only stands in when this placement has no test to take for the skill.
                Status = assessed ? "Assessed" : run != null ? "InProgress" : missions.Any(m => m.SkillId == id && m.CanStart) ? "NotStarted" :
                    confirmed ? "Confirmed" : "Unavailable"
            };
            var weakness = answers.Count == 0 ? .5m : Math.Clamp(1m - correct / (decimal)answers.Count, .1m, 1m);
            result.EstimatedPracticeSets = confirmed ? 0 : LearningPathPacing.EstimatedSetsFor(weakness);
            result.Recommendation = confirmed ? "Đã có bằng chứng độc lập hợp lệ; rút gọn luyện cơ bản và duy trì lịch ôn."
                : assessed ? $"Đúng {correct}/{answers.Count} câu trong phần kiểm tra này. " +
                    (answers.Count < 5 ? "Mẫu còn ít, cần kiểm tra thêm trước khi kết luận. " :
                        weakness >= .6m ? "Nên xem lại kiến thức và ví dụ trước khi luyện. " :
                        weakness >= .3m ? "Ưu tiên luyện các dạng vừa sai, xen kẽ câu ôn. " : "Kết quả ban đầu tốt; cần đủ bằng chứng hợp lệ để xác nhận. ") +
                    $"Dự kiến {Math.Max(1u, result.EstimatedPracticeSets - 1)}–{result.EstimatedPracticeSets + 1} bộ luyện; sẽ điều chỉnh sau từng bộ."
                : result.Status == "Unavailable" ? "Chưa đủ câu mới để kiểm tra; cần giáo viên bổ sung. Có thể bắt đầu học với trạng thái chưa đủ bằng chứng."
                : "Chưa chấm xong phần này; chưa kết luận mức độ nắm vững.";
            report.Skills.Add(result);
            var foundation = issued.Where(i => i.SessionRunId == run?.SessionRunId && i.PrimaryTopicNodeId.ToString() != id && !i.Skipped).ToList();
            if (foundation.Count > 0) result.Recommendation += $" Đã thử thêm {foundation.Count} câu nền tảng; đây là tín hiệu cần xác minh, chưa đủ để kết luận hổng nền hay chưa học. Lộ trình sẽ ưu tiên phần nền tảng có câu sai và tiếp tục kiểm tra.";
        }
        report.TotalSkills = (uint)report.Skills.Count;
        report.AssessedSkills = (uint)report.Skills.Count(s => s.Status is "Assessed" or "Confirmed");
        report.IsComplete = report.TotalSkills > 0 && report.TotalSkills == report.AssessedSkills;
        // Unavailable sections remain Unknown, but cannot prevent the student from finishing the available baseline.
        report.IsComplete = report.TotalSkills > 0 && report.Skills.All(s => s.Status is "Assessed" or "Confirmed" or "Unavailable");
        var scored = report.Skills.Where(s => s.ScoreOutOfTen.HasValue).ToList();
        var total = scored.Sum(s => s.PossibleScore);
        report.ScoreOutOfTen = total > 0 ? Math.Round(10m * scored.Sum(s => s.EarnedScore) / total, 1) : null;
        return report;
    }
}
