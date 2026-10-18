using System.Text.Json;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>
/// Decides the next step after a finished run. Practice is a loop of fixed 6-question sets:
/// at least 80% moves to a harder set and two such sets in a row recommend the independent check; 50-80% repeats the level with
/// missed questions first; under 50% steps down and points back to the lesson. Reviews under 60% send the skill back to practice.
/// </summary>
public static class LearningPathFollowUp
{
    // Called inside evaluator's student lock/transaction; GET agenda never creates sessions.
    public static async Task AddAsync(EduTwinDbContext db, LearningPathSessionRun run, string outcome, DateTime now, CancellationToken ct, decimal? accuracy = null)
    {
        var path = await db.LearningPaths.SingleAsync(p => p.CenterId == run.CenterId && p.LearningPathId == run.Session.LearningPathId, ct);
        if (path.Status != LearningPathStatus.Active || path.PlanJson == null) return;
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson.RootElement.GetRawText(), LearningPathV4Data.Json);
        var milestone = plan?.Milestones.FirstOrDefault(m => m.SkillIds.Contains(run.Session.TopicNodeId.ToString()));
        if (plan == null || milestone == null) return;
        var original = milestone.Missions.FirstOrDefault(m => m.MissionId == run.SessionId.ToString());
        var purpose = original?.Purpose ?? run.Session.Objective;
        var trigger = $"FollowUp:{run.SessionRunId}:{outcome}";
        if (await db.PathDecisions.AnyAsync(d => d.CenterId == run.CenterId && d.LearningPathId == path.LearningPathId && d.TriggerType == trigger, ct)) return;

        var available = await db.Questions.CountAsync(q => q.CenterId == run.CenterId && q.SubjectId == path.SubjectId &&
            q.PrimaryTopicNodeId == run.Session.TopicNodeId && q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active &&
            q.AnswerEvaluationMode != Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual && q.QuestionType != Contracts.CurriculumAndQuestions.QuestionType.Essay, ct);
        var practiceSets = milestone.Missions.Where(m => m.Purpose == "Practice").ToList();
        var lastSet = practiceSets.Count == 0 ? 0u : Math.Max((uint)practiceSets.Count, practiceSets.Max(m => m.SetNumber));
        var checkMission = milestone.Missions.FirstOrDefault(m => m.Purpose == "IndependentCheck");
        var checkSessionId = checkMission == null ? (Guid?)null : Guid.Parse(checkMission.MissionId);
        var checkPassed = checkSessionId.HasValue && await db.LearningPathSessions.AnyAsync(s => s.CenterId == run.CenterId &&
            s.SessionId == checkSessionId.Value && s.Status == LearningPathSessionStatus.Completed, ct);
        var pct = accuracy.HasValue ? (int)Math.Round(accuracy.Value * 100) : 0;
        var skillSetSize = plan.PracticeQuestionsBySkill.GetValueOrDefault(run.Session.TopicNodeId.ToString(), plan.PracticeQuestionsPerSet);
        var setSize = (uint)Math.Min(skillSetSize, (uint)Math.Max(0, available));

        ProposedMissionDto? next = null;
        string explanation;
        string code;
        ProposedMissionDto Set(string title, byte target, bool repair, uint streak) => new()
        {
            MissionId = Guid.NewGuid().ToString(), SkillId = run.Session.TopicNodeId.ToString(), Purpose = "Practice", Title = title,
            QuestionCount = setSize, EstimatedMinutes = Math.Max(1, setSize) * LearningPathPacing.MinutesPerQuestion, ActivityTypes = ["Practice"],
            CanStart = setSize > 0, TargetDifficulty = target, SetNumber = lastSet + 1, PassStreak = streak, IsRepair = repair,
            EstimatedSets = original?.EstimatedSets ?? 0
        };
        ProposedMissionDto Review(int days, string reason) => new()
        {
            MissionId = Guid.NewGuid().ToString(), SkillId = run.Session.TopicNodeId.ToString(), Purpose = "Review", Title = $"Ôn nhớ lại: {milestone.Title}",
            QuestionCount = Math.Min(5u, (uint)Math.Max(0, available)), EstimatedMinutes = Math.Max(1u, Math.Min(5u, (uint)Math.Max(0, available))) * LearningPathPacing.MinutesPerQuestion,
            AvailableFrom = now.AddDays(days), ActivityTypes = ["Practice"], CanStart = available > 0, SelectionReason = reason
        };

        if (outcome == "Passed")
        {
            explanation = "Đạt tự kiểm tra. Hẹn ôn sau 2 ngày để kiểm tra khả năng nhớ lại; buổi ôn không tự cấp xác nhận thành thạo.";
            next = Review(2, explanation); code = "SPACED_RECALL";
        }
        else if (outcome == "NeedsSupport")
        {
            // A late teacher correction can invalidate an unstarted review, but never discards work.
            var future = milestone.Missions.Where(m => m.Purpose == "Review" && m.AvailableFrom > now).Select(m => Guid.Parse(m.MissionId)).ToList();
            // Filter Guid lists in memory: MySQL (Pomelo) cannot translate Contains over a Guid parameter list.
            foreach (var session in (await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId &&
                s.Status == LearningPathSessionStatus.NotStarted).ToListAsync(ct)).Where(s => future.Contains(s.SessionId)))
            { session.Status = LearningPathSessionStatus.Skipped; session.UpdatedAt = now; session.RowVersion++; }
            var missed = await db.IssuedItems.CountAsync(i => i.CenterId == run.CenterId && i.ActivityRun.SessionRunId == run.SessionRunId &&
                i.Attempt != null && i.Attempt.IsCorrect == false, ct);
            milestone.ReadyForCheck = false;
            explanation = $"Tự kiểm tra chưa đạt ({missed} câu sai): thêm bộ rèn lại {setSize} câu, ưu tiên câu từng sai. Đạt 2 bộ liên tiếp từ 80% sẽ mở lại tự kiểm tra bằng câu mới.";
            next = Set($"Rèn lại: {milestone.Title} · bộ {lastSet + 1}", 2, true, 0); code = "TARGETED_REPAIR";
        }
        else if (outcome == "ActivityOnlyCompleted" && purpose == "Review")
        {
            if (accuracy is null or >= LearningPathPacing.ReviewPassRate)
            {
                // Expanding intervals: 7, 14, 28, then monthly.
                var reviews = milestone.Missions.Count(m => m.Purpose == "Review");
                var days = (int)Math.Min(30, 7 * Math.Pow(2, Math.Max(0, reviews - 1)));
                explanation = $"Ôn đạt{(accuracy.HasValue ? $" {pct}%" : "")}: hẹn ôn tiếp sau {days} ngày.";
                next = Review(days, explanation); code = "SPACED_RECALL";
            }
            else
            {
                milestone.ReadyForCheck = false;
                explanation = $"Ôn chỉ đạt {pct}%: kỹ năng cần củng cố lại. Đã thêm bộ rèn lại {setSize} câu; đạt 2 bộ liên tiếp sẽ hẹn ôn lại.";
                next = Set($"Rèn lại: {milestone.Title} · bộ {lastSet + 1}", 2, true, 0); code = "FORGETTING_DETECTED";
            }
        }
        else if (outcome == "ActivityOnlyCompleted" && purpose == "Practice" && accuracy.HasValue && original != null)
        {
            var streak = accuracy >= LearningPathPacing.PassRate ? original.PassStreak + 1 : 0u;
            var target = original.TargetDifficulty ?? 2;
            var setNo = original.SetNumber == 0 ? lastSet : original.SetNumber;
            var notes = "";
            if (original.IsProbe && accuracy >= LearningPathPacing.PassRate)
            {
                var studyIds = milestone.Missions.Where(m => m.Purpose == "Study").Select(m => Guid.Parse(m.MissionId)).ToList();
                foreach (var study in (await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId &&
                    s.Status == LearningPathSessionStatus.NotStarted).ToListAsync(ct)).Where(s => studyIds.Contains(s.SessionId)))
                { study.Status = LearningPathSessionStatus.Skipped; study.UpdatedAt = now; study.RowVersion++; notes = " Bỏ qua phần lý thuyết vì đã làm tốt."; }
            }
            if (streak >= LearningPathPacing.ReadyStreak)
            {
                if (checkPassed)
                {
                    explanation = $"Đã luyện lại đạt {LearningPathPacing.ReadyStreak} bộ liên tiếp. Hẹn ôn sau 2 ngày.";
                    next = Review(2, explanation); code = "RELEARNED";
                }
                else
                {
                    milestone.ReadyForCheck = true;
                    explanation = $"Bộ {setNo} đạt {pct}%: đạt {LearningPathPacing.ReadyStreak} bộ liên tiếp, đã sẵn sàng tự kiểm tra.{notes}"; code = "READY_FOR_CHECK";
                }
            }
            else if (lastSet >= LearningPathPacing.MaxSetsPerSkill)
            {
                milestone.ReadyForCheck = true;
                explanation = $"Đã luyện {lastSet} bộ mà kết quả chưa ổn định. Nên hỏi giáo viên chỗ đang vướng, hoặc làm tự kiểm tra để xác định rõ."; code = "ASK_TEACHER";
            }
            else if (setSize == 0) return;
            else if (accuracy >= LearningPathPacing.PassRate)
            {
                explanation = $"Bộ {setNo} đạt {pct}%: bộ tiếp theo khó hơn. Đạt thêm {LearningPathPacing.ReadyStreak - streak} bộ liên tiếp sẽ mở tự kiểm tra.{notes}";
                next = Set($"{(original.IsRepair ? "Rèn lại" : "Rèn kỹ năng")}: {milestone.Title} · bộ {lastSet + 1}", (byte)Math.Min(5, target + 1), original.IsRepair, streak); code = "STEP_UP";
            }
            else if (accuracy >= LearningPathPacing.RepairRate)
            {
                explanation = $"Bộ {setNo} đạt {pct}%: thêm một bộ cùng mức, ưu tiên câu vừa sai.";
                next = Set($"{(original.IsRepair ? "Rèn lại" : "Rèn kỹ năng")}: {milestone.Title} · bộ {lastSet + 1}", target, original.IsRepair, 0); code = "SAME_LEVEL";
            }
            else
            {
                explanation = $"Bộ {setNo} đạt {pct}%: nên xem lại phần Tìm hiểu và ví dụ mẫu (nút Học lại), rồi làm bộ dễ hơn.";
                next = Set($"{(original.IsRepair ? "Rèn lại" : "Rèn kỹ năng")}: {milestone.Title} · bộ {lastSet + 1}", (byte)Math.Max(1, target - 1), original.IsRepair, 0); code = "STEP_DOWN";
            }
        }
        else return;

        if (next != null)
        {
            next.SelectionReason = explanation;
            // New practice sets sit before the check so the route reads in learning order; reviews go last.
            var checkIndex = checkMission == null ? -1 : milestone.Missions.IndexOf(checkMission);
            if (next.Purpose == "Practice" && checkIndex >= 0) milestone.Missions.Insert(checkIndex, next); else milestone.Missions.Add(next);
            db.LearningPathSessions.Add(new LearningPathSession { CenterId = run.CenterId, LearningPathId = path.LearningPathId,
                SessionId = Guid.Parse(next.MissionId), TopicNodeId = run.Session.TopicNodeId, ClassId = run.Session.ClassId,
                PhaseNumber = run.Session.PhaseNumber, WeekNumber = run.Session.WeekNumber,
                SessionOrder = (await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId).MaxAsync(s => (uint?)s.SessionOrder, ct) ?? 0) + 1,
                Title = next.Title, Objective = next.Purpose, Rationale = explanation, MinimumQuestions = next.QuestionCount,
                RequiredCorrectRate = .8m, PolicyVersion = LearningPathV4Data.Policy, CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
        }
        path.PlanJson = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json); path.UpdatedAt = now; path.RowVersion++;
        var evidenceIds = await db.EvidenceAssessments.Where(e => e.CenterId == run.CenterId && e.Attempt.SessionRunId == run.SessionRunId)
            .Select(e => e.EvidenceAssessmentId).ToListAsync(ct);
        db.PathDecisions.Add(new PathDecision { CenterId = run.CenterId, LearningPathId = path.LearningPathId, SessionRunId = run.SessionRunId,
            TriggerType = trigger, DecisionSummary = explanation, PolicyVersion = LearningPathV4Data.Policy,
            ReasonCodes = JsonSerializer.SerializeToDocument(new[] { code }),
            EvidenceReferenceIds = JsonSerializer.SerializeToDocument(evidenceIds), CreatedAt = now, UpdatedAt = now, RowVersion = 1 });
    }
}
