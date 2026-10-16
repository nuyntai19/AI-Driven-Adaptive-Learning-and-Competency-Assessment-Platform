using System.Text.Json;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public static class LearningPathFollowUp
{
    // Called inside evaluator's student lock/transaction; GET agenda never creates sessions.
    public static async Task AddAsync(EduTwinDbContext db, LearningPathSessionRun run, string outcome, DateTime now, CancellationToken ct)
    {
        var path = await db.LearningPaths.SingleAsync(p => p.CenterId == run.CenterId && p.LearningPathId == run.Session.LearningPathId, ct);
        if (path.Status != LearningPathStatus.Active || path.PlanJson == null) return;
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson.RootElement.GetRawText(), LearningPathV4Data.Json);
        var milestone = plan?.Milestones.FirstOrDefault(m => m.SkillIds.Contains(run.Session.TopicNodeId.ToString()));
        if (milestone == null) return;
        var original = milestone.Missions.FirstOrDefault(m => m.MissionId == run.SessionId.ToString());
        var review = outcome == "Passed" || outcome == "ActivityOnlyCompleted" && original?.Purpose == "Review";
        if (!review && outcome != "NeedsSupport") return;
        var trigger = $"FollowUp:{run.SessionRunId}:{outcome}";
        if (await db.PathDecisions.AnyAsync(d => d.CenterId == run.CenterId && d.LearningPathId == path.LearningPathId && d.TriggerType == trigger, ct)) return;
        // A late teacher correction can invalidate an unstarted review, but never discards work.
        if (outcome == "NeedsSupport")
        {
            var future = milestone.Missions.Where(m => m.Purpose == "Review" && m.AvailableFrom > now).Select(m => Guid.Parse(m.MissionId)).ToList();
            if (future.Count > 0)
            {
                foreach (var session in await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && future.Contains(s.SessionId) && s.Status == LearningPathSessionStatus.NotStarted).ToListAsync(ct))
                { session.Status = LearningPathSessionStatus.Skipped; session.UpdatedAt = now; session.RowVersion++; }
            }
        }
        var count = await db.Questions.CountAsync(q => q.CenterId == run.CenterId && q.SubjectId == path.SubjectId &&
            q.PrimaryTopicNodeId == run.Session.TopicNodeId && q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active &&
            q.AnswerEvaluationMode != Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual && q.QuestionType != Contracts.CurriculumAndQuestions.QuestionType.Essay, ct);
        var days = original?.Purpose == "Review" ? 7 : 2;
        var explanation = review ? $"Đã làm tốt buổi trước; hẹn ôn sau {days} ngày để kiểm tra khả năng nhớ lại. Buổi ôn không tự cấp xác nhận thành thạo."
            : "Kết quả tự kiểm tra chưa đạt: đã thêm một bộ rèn lại đúng kỹ năng, ưu tiên câu từng sai trước khi tiếp tục.";
        var mission = new ProposedMissionDto { MissionId = Guid.NewGuid().ToString(), SkillId = run.Session.TopicNodeId.ToString(),
            Purpose = review ? "Review" : "Practice", Title = $"{(review ? "Ôn nhớ lại" : "Rèn lại")}: {milestone.Title}",
            QuestionCount = (uint)Math.Min(count, 6), EstimatedMinutes = (uint)Math.Max(1, Math.Min(count, 6)) * 2,
            AvailableFrom = review ? now.AddDays(days) : null, ActivityTypes = ["Practice"], CanStart = count > 0, SelectionReason = explanation };
        milestone.Missions.Add(mission);
        db.LearningPathSessions.Add(new LearningPathSession { CenterId = run.CenterId, LearningPathId = path.LearningPathId,
            SessionId = Guid.Parse(mission.MissionId), TopicNodeId = run.Session.TopicNodeId, ClassId = run.Session.ClassId,
            PhaseNumber = run.Session.PhaseNumber, WeekNumber = run.Session.WeekNumber,
            SessionOrder = (await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId).MaxAsync(s => (uint?)s.SessionOrder, ct) ?? 0) + 1,
            Title = mission.Title, Objective = mission.Purpose, Rationale = explanation, MinimumQuestions = mission.QuestionCount,
            RequiredCorrectRate = .8m, PolicyVersion = LearningPathV4Data.Policy, CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
        path.PlanJson = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json); path.UpdatedAt = now; path.RowVersion++;
        var evidenceIds = await db.EvidenceAssessments.Where(e => e.CenterId == run.CenterId && e.Attempt.SessionRunId == run.SessionRunId)
            .Select(e => e.EvidenceAssessmentId).ToListAsync(ct);
        db.PathDecisions.Add(new PathDecision { CenterId = run.CenterId, LearningPathId = path.LearningPathId, SessionRunId = run.SessionRunId,
            TriggerType = trigger, DecisionSummary = explanation, PolicyVersion = LearningPathV4Data.Policy,
            ReasonCodes = JsonSerializer.SerializeToDocument(new[] { review ? "SPACED_RECALL" : "TARGETED_REPAIR" }),
            EvidenceReferenceIds = JsonSerializer.SerializeToDocument(evidenceIds), CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
    }
}
