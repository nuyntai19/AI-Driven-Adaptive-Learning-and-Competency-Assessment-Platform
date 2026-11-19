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
        var milestone = plan?.Milestones.FirstOrDefault(m => m.Missions.Any(x => x.MissionId == run.SessionId.ToString()));
        if (plan == null || milestone == null) return;
        // Placement gathers a baseline across the chosen scope; remediation starts in the next accepted route.
        if (plan.IsPlacement) return;
        var original = milestone.Missions.FirstOrDefault(m => m.MissionId == run.SessionId.ToString());
        if (original?.IsMilestoneExam == true) { await LearningPathExamFollowUp.AddAsync(db, path, plan, run, now, ct); return; }
        if (original is { AssessedSkillIds.Count: > 0 } && !original.AssessedSkillIds.Contains(run.Session.TopicNodeId.ToString())) return;
        var purpose = original?.Purpose ?? run.Session.Objective;
        var trigger = $"FollowUp:{run.SessionRunId}:{outcome}";
        if (await db.PathDecisions.AnyAsync(d => d.CenterId == run.CenterId && d.LearningPathId == path.LearningPathId && d.TriggerType == trigger, ct)) return;

        var available = await db.Questions.CountAsync(q => q.CenterId == run.CenterId && q.SubjectId == path.SubjectId &&
            q.PrimaryTopicNodeId == run.Session.TopicNodeId && q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active &&
            q.AnswerEvaluationMode != Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual && q.QuestionType != Contracts.CurriculumAndQuestions.QuestionType.Essay, ct);
        var practiceSets = milestone.Missions.Where(m => m.Purpose == "Practice").ToList();
        var lastSet = practiceSets.Count == 0 ? 0u : Math.Max((uint)practiceSets.Count, practiceSets.Max(m => m.SetNumber));
        var mainNumbers = LearningPathRemediation.MainSetNumbers(milestone.Missions);
        var nextMainNumber = (uint)milestone.Missions.Count(mission => mission.Purpose == "Practice" && mission.RemediationRound == 0 && mission.SkillId == run.Session.TopicNodeId.ToString()) + 1;
        var checkMission = milestone.Missions.FirstOrDefault(m => m.Purpose == "IndependentCheck");
        var checkSessionId = checkMission == null ? (Guid?)null : Guid.Parse(checkMission.MissionId);
        var checkPassed = checkSessionId.HasValue && await db.LearningPathSessions.AnyAsync(s => s.CenterId == run.CenterId &&
            s.SessionId == checkSessionId.Value && s.Status == LearningPathSessionStatus.Completed, ct);
        var pct = accuracy.HasValue ? (int)Math.Round(accuracy.Value * 100) : 0;
        var skillSetSize = plan.PracticeQuestionsBySkill.GetValueOrDefault(run.Session.TopicNodeId.ToString(), plan.PracticeQuestionsPerSet);
        var setSize = (uint)Math.Min(skillSetSize, (uint)Math.Max(0, available));

        ProposedMissionDto? next = null;
        ProposedMissionDto? extra = null;
        ProposedMissionDto? supportStudy = null;
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

        if (outcome == "NeedsSupport")
        {
            // Preserve late-correction invalidation for both broad and focused repair.
            var future = milestone.Missions.Where(m => m.Purpose == "Review" && m.AvailableFrom > now).Select(m => Guid.Parse(m.MissionId)).ToHashSet();
            foreach (var session in (await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId &&
                s.Status == LearningPathSessionStatus.NotStarted).ToListAsync(ct)).Where(s => future.Contains(s.SessionId)))
            { session.Status = LearningPathSessionStatus.Skipped; session.UpdatedAt = now; session.RowVersion++; }
        }
        var observations = original != null && (purpose is "Practice" or "Review" || outcome == "NeedsSupport")
            ? await LearningPathRemediation.ObserveAsync(db, run, original, milestone.Missions, ct)
            : (Current: new List<LearningPathRemediation.Observation>(), History: new List<LearningPathRemediation.Observation>());
        var weakFocuses = LearningPathRemediation.WeakFocuses(observations.Current);
        var focuses = (original?.RemediationFocuses.Count > 0 ? original.RemediationFocuses : weakFocuses).ToList();
        var repairResult = outcome == "NeedsSupport" || outcome == "ActivityOnlyCompleted" &&
            (purpose == "Practice" || purpose == "Review" && accuracy < LearningPathPacing.ReviewPassRate);
        if (repairResult && original != null && focuses.Count > 0)
        {
            milestone.ReadyForCheck = false;
            if (purpose == "Review") milestone.IsSelfAssessedCheck = false;
            if (checkMission == null)
                extra = new ProposedMissionDto {
                    MissionId = Guid.NewGuid().ToString(), SkillId = run.Session.TopicNodeId.ToString(), Purpose = "IndependentCheck",
                    ActivityTypes = ["IndependentCheck"], Title = $"Tự kiểm tra: {milestone.Title}", QuestionCount = 5,
                    EstimatedMinutes = 10, CanStart = available >= 3,
                    SelectionReason = "Tự kiểm tra cả chủ đề sau khi củng cố phần yếu và luyện tổng hợp; luyện nhánh không tự xác nhận thành thạo."
                };
            var round = original.RemediationRound + 1;
            var focusPass = LearningPathRemediation.Secure(focuses, observations.Current, 1, window: null);
            var streak = accuracy >= LearningPathPacing.PassRate && focusPass ? original.PassStreak + 1 : 0u;
            var secure = LearningPathRemediation.Secure(focuses, observations.History);
            var names = string.Join(" + ", focuses);
            var eligible = await LearningPathV4Data.QuestionsAsync(db, run.CenterId, run.StudentId, path.SubjectId,
                new[] { run.Session.TopicNodeId }, run.Session.ClassId, ct);
            var smallBank = focuses.Any(focus => eligible.Count(question => LearningPathRemediation.Focuses(question).Contains(focus, StringComparer.OrdinalIgnoreCase)) < 3);
            if (original.RemediationRound > 0 && streak >= LearningPathPacing.ReadyStreak && secure && lastSet < LearningPathPacing.MaxSetsPerSkill)
            {
                var root = original.RemediationRootMissionId ?? original.MissionId;
                var unfinishedLessons = milestone.Missions.Where(mission => mission.Purpose == "Study" && mission.RemediationRootMissionId == root)
                    .Select(mission => Guid.Parse(mission.MissionId)).ToHashSet();
                foreach (var lesson in (await db.LearningPathSessions.Where(session => session.CenterId == run.CenterId && session.LearningPathId == path.LearningPathId &&
                    session.Status == LearningPathSessionStatus.NotStarted).ToListAsync(ct)).Where(session => unfinishedLessons.Contains(session.SessionId)))
                { lesson.Status = LearningPathSessionStatus.Skipped; lesson.UpdatedAt = now; lesson.RowVersion++; }
                explanation = $"Đã củng cố {names}: đạt ít nhất 80% trên tối thiểu 3 câu khác nhau mỗi phần và 2 bộ liên tiếp. Quay lại bộ luyện tổng hợp; chỉ bài tự kiểm tra mới xác nhận nắm vững cả chủ đề.";
                next = Set($"Rèn kỹ năng: {milestone.Title} · bộ {nextMainNumber}", original.TargetDifficulty ?? 2, false, 0);
                code = "FOCUS_REPAIR_RETURN";
            }
            else if (lastSet >= LearningPathPacing.MaxSetsPerSkill || original.RemediationRound > 0 && (smallBank || original.RemediationRound >= LearningPathRemediation.MaxRounds))
            {
                explanation = smallBank
                    ? $"Chưa thể xác nhận {names}: ngân hàng chưa đủ 3 câu khác nhau cho mỗi phần. Không lặp vô hạn hoặc coi nhớ đáp án là đã vững; hãy nhờ giáo viên bổ sung câu và hỗ trợ phần này."
                    : $"Đã luyện {lastSet} bộ, trong đó có {original.RemediationRound} vòng ôn {names}. Tạm dừng thêm bộ tự động; nên xem lại ví dụ và nhờ giáo viên hỗ trợ hoặc điều chỉnh lộ trình trước khi luyện tiếp. Chưa xác nhận đã vững cả chủ đề.";
                code = smallBank ? "FOCUS_REPAIR_NEEDS_QUESTIONS" : "FOCUS_REPAIR_SUPPORT";
            }
            else
            {
                var target = original.TargetDifficulty ?? 2;
                if (accuracy < LearningPathPacing.RepairRate) target = (byte)Math.Max(1, target - 1);
                next = Set($"Rèn lại bằng bài tập: {names} · vòng {round}", target, true, streak);
                next.RemediationFocuses = focuses;
                next.RemediationRound = round;
                next.RemediationSourceMissionId = original.MissionId;
                next.RemediationRootMissionId = original.RemediationRootMissionId ?? original.MissionId;
                explanation = $"Bộ vừa học cho thấy cần củng cố {names}. Học lại qua ví dụ, rồi luyện vòng {round}: khoảng 80% câu đúng phần này và một ít câu đối chiếu, ưu tiên câu khác chưa làm. Cần 2 bộ liên tiếp từ 80% và ít nhất 3 câu khác nhau mỗi phần trước khi trở lại luyện tổng hợp."
                    + (smallBank ? " Ngân hàng còn ít câu ở phần yếu; kết quả sẽ chưa đủ để kết luận đã vững." : "");
                if (original.RemediationRound == 0 || accuracy < LearningPathPacing.RepairRate)
                    supportStudy = new ProposedMissionDto {
                        MissionId = Guid.NewGuid().ToString(), SkillId = next.SkillId, Purpose = "Study", Title = $"Ôn lại: {names}",
                        EstimatedMinutes = 5, ActivityTypes = ["Study"], IsRepair = true,
                        RemediationFocuses = focuses, RemediationRound = round,
                        RemediationSourceMissionId = original.MissionId, RemediationRootMissionId = next.RemediationRootMissionId,
                        SelectionReason = "Dùng nội dung đã biên soạn và ví dụ đúng phần cần củng cố. Có thể xem lại lời giải các câu vừa sai qua nút Học lại."
                    };
                code = "FOCUS_REPAIR_BRANCH";
            }
        }
        else if (outcome == "Passed")
        {
            explanation = "Đạt tự kiểm tra. Hẹn ôn sau 2 ngày để kiểm tra khả năng nhớ lại; buổi ôn không tự cấp xác nhận thành thạo.";
            next = Review(2, explanation); code = "SPACED_RECALL";
        }
        else if (outcome == "Inconclusive")
        {
            explanation = "Mẫu kiểm tra còn ít: chưa kết luận, không bắt học lại từ đầu. Hẹn thử lại sau hai ngày học; bằng chứng độc lập được tích lũy theo câu khác nhau.";
            next = new ProposedMissionDto { MissionId = Guid.NewGuid().ToString(), SkillId = run.Session.TopicNodeId.ToString(),
                Purpose = "IndependentCheck", Title = $"Kiểm tra bổ sung: {milestone.Title}", IsProbe = true, QuestionCount = 3,
                EstimatedMinutes = 6, AvailableFrom = now.AddDays(2), ActivityTypes = ["IndependentCheck"], CanStart = available >= 3 };
            code = "INCONCLUSIVE_RECHECK";
        }
        else if (outcome == "NeedsSupport")
        {
            var missed = await db.IssuedItems.CountAsync(i => i.CenterId == run.CenterId && i.ActivityRun.SessionRunId == run.SessionRunId &&
                i.Attempt != null && i.Attempt.IsCorrect == false, ct);
            milestone.ReadyForCheck = false;
            explanation = $"Tự kiểm tra chưa đạt ({missed} câu sai): thêm bộ rèn lại {setSize} câu, ưu tiên câu từng sai. Đạt 2 bộ liên tiếp từ 80% sẽ mở lại tự kiểm tra bằng câu mới.";
            next = Set($"Rèn lại: {milestone.Title} · bộ {nextMainNumber}", 2, true, 0); code = "TARGETED_REPAIR";
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
            else if (original?.IsSelfAssessedCheck == true)
            {
                // The self-assessment did not hold: the part becomes regular learning with a repair set and a self-check.
                milestone.IsSelfAssessedCheck = false; milestone.ReadyForCheck = false;
                explanation = $"Ôn xác nhận đạt {pct}%: phần em tự đánh giá đã vững cần củng cố thêm. Đã thêm bộ rèn lại {setSize} câu và bài tự kiểm tra; đạt 2 bộ liên tiếp nên làm tự kiểm tra.";
                next = Set($"Rèn lại: {milestone.Title} · bộ {nextMainNumber}", 2, true, 0); code = "SELF_ASSESSMENT_NOT_CONFIRMED";
                if (checkMission == null)
                    extra = new ProposedMissionDto
                    {
                        MissionId = Guid.NewGuid().ToString(), SkillId = run.Session.TopicNodeId.ToString(), Purpose = "IndependentCheck", ActivityTypes = ["IndependentCheck"],
                        Title = $"Tự kiểm tra: {milestone.Title}", QuestionCount = 5, EstimatedMinutes = 15, CanStart = available >= 5,
                        SelectionReason = available >= 5 ? "Kiểm tra bằng câu chưa lộ đáp án; cần ít nhất 5 câu độc lập, đạt 80%." : $"Hiện có {available}/5 câu hợp lệ; chưa đủ để xác nhận kỹ năng."
                    };
            }
            else
            {
                milestone.ReadyForCheck = false;
                explanation = $"Ôn chỉ đạt {pct}%: kỹ năng cần củng cố lại. Đã thêm bộ rèn lại {setSize} câu; đạt 2 bộ liên tiếp sẽ hẹn ôn lại.";
                next = Set($"Rèn lại: {milestone.Title} · bộ {nextMainNumber}", 2, true, 0); code = "FORGETTING_DETECTED";
            }
        }
        else if (outcome == "ActivityOnlyCompleted" && purpose == "Practice" && accuracy.HasValue && original != null)
        {
            var streak = accuracy >= LearningPathPacing.PassRate ? original.PassStreak + 1 : 0u;
            var target = original.TargetDifficulty ?? 2;
            var setNo = mainNumbers.GetValueOrDefault(original.MissionId, original.SetNumber);
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
                milestone.ReadyForCheck = false;
                explanation = $"Đã luyện {lastSet} bộ mà kết quả chưa ổn định. Nên hỏi giáo viên chỗ đang vướng, hoặc làm tự kiểm tra để xác định rõ."; code = "ASK_TEACHER";
            }
            else if (setSize == 0) return;
            else if (accuracy >= LearningPathPacing.PassRate)
            {
                explanation = $"Bộ {setNo} đạt {pct}%: bộ tiếp theo khó hơn. Đạt thêm {LearningPathPacing.ReadyStreak - streak} bộ liên tiếp sẽ mở tự kiểm tra.{notes}";
                next = Set($"{(original.IsRepair ? "Rèn lại" : "Rèn kỹ năng")}: {milestone.Title} · bộ {nextMainNumber}", (byte)Math.Min(5, target + 1), original.IsRepair, streak); code = "STEP_UP";
            }
            else if (accuracy >= LearningPathPacing.RepairRate)
            {
                explanation = $"Bộ {setNo} đạt {pct}%: thêm một bộ cùng mức, ưu tiên câu vừa sai.";
                next = Set($"{(original.IsRepair ? "Rèn lại" : "Rèn kỹ năng")}: {milestone.Title} · bộ {nextMainNumber}", target, original.IsRepair, 0); code = "SAME_LEVEL";
            }
            else
            {
                explanation = $"Bộ {setNo} đạt {pct}%: nên xem lại phần Tìm hiểu và ví dụ mẫu (nút Học lại), rồi làm bộ dễ hơn.";
                next = Set($"{(original.IsRepair ? "Rèn lại" : "Rèn kỹ năng")}: {milestone.Title} · bộ {nextMainNumber}", (byte)Math.Max(1, target - 1), original.IsRepair, 0); code = "STEP_DOWN";
            }
        }
        else return;

        foreach (var (mission, reason) in new[] { (supportStudy, supportStudy?.SelectionReason ?? ""), (next, explanation), (extra, extra?.SelectionReason ?? "") })
        {
            if (mission == null) continue;
            mission.SelectionReason = reason;
            if (original?.SourceExamAssignmentId is { } examId && mission.IsRepair)
                mission.SourceExamAssignmentId = examId;
            // New practice sets sit before the check so the route reads in learning order; reviews and checks go last.
            var checkIndex = checkMission == null ? -1 : milestone.Missions.IndexOf(checkMission);
            var sourceIndex = milestone.Missions.FindIndex(item => item.MissionId == original?.MissionId);
            if (mission.RemediationSourceMissionId != null && sourceIndex >= 0)
                milestone.Missions.Insert(sourceIndex + (mission == next && supportStudy != null ? 2 : 1), mission);
            else if (mission.Purpose == "Practice" && checkIndex >= 0) milestone.Missions.Insert(checkIndex, mission); else milestone.Missions.Add(mission);
            db.LearningPathSessions.Add(new LearningPathSession { CenterId = run.CenterId, LearningPathId = path.LearningPathId,
                SessionId = Guid.Parse(mission.MissionId), TopicNodeId = run.Session.TopicNodeId, ClassId = run.Session.ClassId,
                PhaseNumber = run.Session.PhaseNumber, WeekNumber = run.Session.WeekNumber,
                SessionOrder = Math.Max((await db.LearningPathSessions.Where(s => s.CenterId == run.CenterId && s.LearningPathId == path.LearningPathId).MaxAsync(s => (uint?)s.SessionOrder, ct) ?? 0),
                    db.LearningPathSessions.Local.Where(s => s.LearningPathId == path.LearningPathId).Select(s => s.SessionOrder).DefaultIfEmpty(0u).Max()) + 1,
                Title = mission.Title, Objective = mission.Purpose, Rationale = reason, MinimumQuestions = mission.Purpose == "IndependentCheck" ? 5u : mission.QuestionCount,
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
