using System.Text.Json;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public static class LearningPathExamFollowUp
{
    // Runs under the evaluator's student lock. Grade each actual question's skill, never attribute a mixed exam to its first skill.
    public static async Task AddAsync(EduTwinDbContext db, LearningPath path, PathProposalDto plan, LearningPathSessionRun run, DateTime now, CancellationToken ct)
    {
        var trigger = $"ExamFollowUp:{run.SessionRunId}";
        if (await db.PathDecisions.AnyAsync(d => d.CenterId == run.CenterId && d.LearningPathId == path.LearningPathId && d.TriggerType == trigger, ct)) return;
        var answers = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == run.CenterId && i.ActivityRun.SessionRunId == run.SessionRunId && i.Attempt != null)
            .Select(i => new { i.Attempt!.Question.PrimaryTopicNodeId, i.Attempt.IsCorrect, i.Attempt.AttemptId }).ToListAsync(ct);
        var ids = answers.Select(a => a.AttemptId).ToList();
        var corrections = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == run.CenterId && ids.Contains(a.AttemptId)).ToDictionaryAsync(a => a.AttemptId, ct);
        var weak = answers.GroupBy(a => a.PrimaryTopicNodeId).Where(g => g.Count(a => corrections.GetValueOrDefault(a.AttemptId)?.OverrideIsCorrect ?? a.IsCorrect ?? false) / (decimal)g.Count() < run.Session.RequiredCorrectRate)
            .Select(g => g.Key).ToList();
        uint order = (await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId).MaxAsync(s => (uint?)s.SessionOrder, ct) ?? 0);
        foreach (var skillId in weak)
        {
            var id = skillId.ToString();
            var milestone = plan.Milestones.FirstOrDefault(m => m.SkillIds.Count == 1 && m.SkillIds[0] == id && m.Missions.Any(x => !x.IsMilestoneExam));
            if (milestone == null) continue;
            var existingIds = milestone.Missions.Where(m => m.IsRepair).Select(m => Guid.Parse(m.MissionId)).ToList();
            var pending = (await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId && s.Status == LearningPathSessionStatus.NotStarted).Select(s => s.SessionId).ToListAsync(ct))
                .Any(existingIds.Contains);
            if (pending) continue;
            var pool = await LearningPathV4Data.QuestionsAsync(db, run.CenterId, run.StudentId, path.SubjectId, new[] { skillId }, run.Session.ClassId, ct);
            var size = Math.Min((uint)pool.Count, Math.Min(plan.PracticeQuestionsPerSet, plan.AvailableMinutesPerDay / 2));
            var repair = new ProposedMissionDto { MissionId = Guid.NewGuid().ToString(), SkillId = id, Purpose = "Practice", IsRepair = true,
                Title = $"Rèn lại sau mốc thi: {milestone.Title}", QuestionCount = size, EstimatedMinutes = size * 2, ActivityTypes = ["Practice"], CanStart = size > 0,
                SelectionReason = "Kết quả mốc thi cho thấy kỹ năng này cần củng cố; các kỹ năng độc lập khác vẫn học tiếp được." };
            var check = new ProposedMissionDto { MissionId = Guid.NewGuid().ToString(), SkillId = id, Purpose = "IndependentCheck", IsRepair = true,
                Title = $"Kiểm tra lại: {milestone.Title}", QuestionCount = 5, EstimatedMinutes = 10, ActivityTypes = ["IndependentCheck"], CanStart = pool.Count >= 5,
                SelectionReason = "Làm phần rèn lại rồi kiểm tra bằng câu mới; thiếu câu thì giữ trạng thái chưa đủ bằng chứng." };
            milestone.Missions.Add(repair); milestone.Missions.Add(check); milestone.ReadyForCheck = false;
            foreach (var mission in new[] { repair, check })
                db.LearningPathSessions.Add(new LearningPathSession { SessionId = Guid.Parse(mission.MissionId), CenterId = run.CenterId,
                    LearningPathId = path.LearningPathId, TopicNodeId = skillId, ClassId = run.Session.ClassId, Title = mission.Title, Objective = mission.Purpose,
                    Rationale = mission.SelectionReason, PhaseNumber = milestone.Order, SessionOrder = ++order, MinimumQuestions = mission.QuestionCount,
                    RequiredCorrectRate = run.Session.RequiredCorrectRate, PolicyVersion = LearningPathV4Data.Policy, CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
        }
        path.PlanJson = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json); path.UpdatedAt = now; path.RowVersion++;
        db.PathDecisions.Add(new PathDecision { CenterId = run.CenterId, LearningPathId = path.LearningPathId, SessionRunId = run.SessionRunId,
            TriggerType = trigger, DecisionSummary = $"Đã phân tích mốc thi theo từng kỹ năng: {weak.Count} phần cần củng cố. Câu chưa làm không tự xác nhận năng lực; không khóa toàn tuyến.",
            ReasonCodes = JsonSerializer.SerializeToDocument(new[] { "EXAM_SKILL_FOLLOW_UP" }), PolicyVersion = LearningPathV4Data.Policy,
            CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
    }
}
