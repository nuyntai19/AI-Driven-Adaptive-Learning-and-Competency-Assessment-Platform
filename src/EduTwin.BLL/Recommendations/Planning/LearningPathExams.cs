using System.Text.Json;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed partial class LearningPathV4Planner
{
    private async Task ReconcileClassExamEnrollmentAsync(Guid center, Guid student, Guid subject, Guid? classId, CancellationToken ct)
    {
        var classes = await db.Assignments.AsNoTracking().Where(a => a.CenterId == center && a.Class!.SubjectId == subject &&
            (!classId.HasValue || a.ClassId == classId) && a.Kind != AssignmentKind.Regular && a.Status == AssignmentStatus.Published &&
            a.TargetMode == TargetSource.WholeClass && a.ExamAt > Now &&
            db.ClassStudents.Any(m => m.CenterId == center && m.StudentId == student && m.ClassId == a.ClassId &&
                m.Status == Contracts.Organization.ClassStudentStatus.Active && m.RemovedAt == null &&
                m.Class.Status == Contracts.Organization.ClassStatus.Active && m.Class.LearningScope == Contracts.Organization.ClassLearningScope.Current) &&
            (!db.AssignmentTargets.Any(t => t.CenterId == center && t.StudentId == student && t.AssignmentId == a.AssignmentId) ||
             !db.StudentAssignmentProgresses.Any(p => p.CenterId == center && p.StudentId == student && p.AssignmentId == a.AssignmentId)))
            .Select(a => a.ClassId).Distinct().ToListAsync(ct);
        if (classes.Count == 0) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StudentLockHelper.AcquireStudentLockAsync(db, center, student, ct);
        foreach (var id in classes)
            await Assignments.ClassExamEnrollment.StageAsync(db, center, id, new[] { student }, Now, ct, student);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task RefreshExamDefinitionsAsync(Guid center, Guid student, LearningPath path, PathProposalDto plan,
        Guid? classId, bool deadlineChanged, CancellationToken ct)
    {
        var remaining = plan.DeadlineDate.HasValue ? LearningPathCalendar.LocalDate(plan.DeadlineDate.Value, plan.Schedule.TimeZoneId).DayNumber
            - LearningPathCalendar.LocalDate(Now, plan.Schedule.TimeZoneId).DayNumber : int.MaxValue;
        var nearMock = remaining >= 0 && remaining <= plan.Schedule.MockExamDays;
        foreach (var mission in plan.Milestones.SelectMany(m => m.Missions))
        {
            var session = path.Sessions.FirstOrDefault(s => s.SessionId.ToString() == mission.MissionId);
            if (deadlineChanged && mission.IsMilestoneExam && mission.IsProbe && session?.Status == LearningPathSessionStatus.Completed)
                mission.SkipReason = "GoalScheduleChanged";
            if (session?.Status != LearningPathSessionStatus.NotStarted) continue;
            if (mission.IsMilestoneExam && mission.IsProbe && (deadlineChanged || !nearMock || mission.EstimatedMinutes > plan.AvailableMinutesPerDay))
            {
                session.Status = LearningPathSessionStatus.Skipped; session.UpdatedAt = Now; session.RowVersion++;
                mission.CanStart = false; mission.SkipReason = "GoalScheduleChanged";
            }
            if (mission.SkipReason == "ExamPhaseLowPriority")
            { mission.SkipReason = null; mission.CanStart = true; mission.Deferred = false; }
        }
        var map = await LearningPathV4Data.MapAsync(db, center, student, path.SubjectId, classId, Now, ct);
        var nodes = await LearningPathV4Data.NodesAsync(db, center, student, path.SubjectId, classId, ct);
        var pool = await LearningPathV4Data.QuestionsAsync(db, center, student, path.SubjectId, nodes.Select(n => n.NodeId).ToList(), classId, ct);
        var used = (await db.Attempts.AsNoTracking().Where(a => a.CenterId == center && a.StudentId == student).Select(a => a.QuestionId).ToListAsync(ct)).ToHashSet();
        var ordered = plan.Milestones.SelectMany(m => m.SkillIds).Distinct().Select(id => map.Skills.FirstOrDefault(s => s.SkillId == id)).OfType<SkillCapabilityStatusDto>().ToList();
        var weighted = nodes.Where(n => n.ExamImportance > 0).Select(n => n.ExamImportance).Order().ToList();
        var neutral = weighted.Count > 0 ? weighted[weighted.Count / 2] : 50m;
        AddExamPreparationMilestones(plan, ordered, pool, used, nodes.ToDictionary(n => n.NodeId.ToString(), n => n.ExamImportance > 0 ? n.ExamImportance : neutral),
            path.Sessions.Where(s => s.Status != LearningPathSessionStatus.NotStarted).Select(s => s.SessionId.ToString()).ToHashSet());
        var order = path.Sessions.Select(s => s.SessionOrder).DefaultIfEmpty(0u).Max();
        foreach (var milestone in plan.Milestones)
        foreach (var mission in milestone.Missions)
        {
            var session = path.Sessions.FirstOrDefault(s => s.SessionId.ToString() == mission.MissionId);
            if (session != null) continue;
            db.LearningPathSessions.Add(new LearningPathSession { SessionId = Guid.Parse(mission.MissionId), CenterId = center,
                LearningPathId = path.LearningPathId, TopicNodeId = ulong.Parse(mission.SkillId), ClassId = classId,
                Title = mission.Title, Objective = mission.Purpose, Rationale = mission.SelectionReason, PhaseNumber = milestone.Order,
                SessionOrder = ++order, MinimumQuestions = mission.QuestionCount, RequiredCorrectRate = .8m,
                PolicyVersion = LearningPathV4Data.Policy, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
        }
    }

    private void AddExamPreparationMilestones(PathProposalDto plan, List<SkillCapabilityStatusDto> ordered,
        List<DAL.CurriculumAndQuestions.Question> questions, HashSet<ulong> used, Dictionary<string, decimal> importance,
        IReadOnlySet<string>? preserved = null)
    {
        // Exam preparation has progress gates even without a date. Dated goals also reserve a diagnostic mock.
        if (plan.IsPlacement) return;
        var remaining = plan.DeadlineDate.HasValue ? LearningPathCalendar.LocalDate(plan.DeadlineDate.Value, plan.Schedule.TimeZoneId).DayNumber - LearningPathCalendar.LocalDate(Now, plan.Schedule.TimeZoneId).DayNumber : int.MaxValue;
        // A stage gate is unlocked by learning progress, even when no examination date was chosen.
        if ((plan.GoalType == "ExamPrep" || plan.DeadlineDate.HasValue) && plan.AvailableMinutesPerDay >= 10)
            foreach (var milestone in plan.Milestones)
            {
                if (milestone.Missions.Any(m => m.IsMilestoneExam && !m.IsProbe)) continue;
                var check = milestone.Missions.FirstOrDefault(m => m.Purpose == "IndependentCheck" && !m.IsMilestoneExam && m.SkipReason == null);
                if (check == null || preserved?.Contains(check.MissionId) == true) continue;
                if (check.IsProbe)
                {
                    check = JsonSerializer.Deserialize<ProposedMissionDto>(JsonSerializer.Serialize(check, LearningPathV4Data.Json), LearningPathV4Data.Json)!;
                    check.MissionId = Guid.NewGuid().ToString();
                }
                check.IsMilestoneExam = true; check.TimeLimitMinutes = 10; check.EstimatedMinutes = 10; check.IsProbe = false;
                check.Title = $"Kiểm tra cuối chặng: {milestone.Title}";
                check.SelectionReason = "Mốc sau phần học/luyện của kỹ năng, bấm giờ 10 phút; kết quả chỉ khóa phần phụ thuộc, không khóa kỹ năng độc lập.";
                milestone.Missions.Remove(check); milestone.Missions.Add(check);
            }
        if (remaining < 0 || remaining > plan.Schedule.MockExamDays || plan.AvailableMinutesPerDay < 10 ||
            plan.Milestones.SelectMany(m => m.Missions).Any(m => m.IsMilestoneExam && m.IsProbe && m.SkipReason == null)) return;
        if (importance.Count == 0) return;
        var threshold = importance.Values.Order().ElementAt(importance.Count / 2);
        var protectedSkills = ordered.Where(s => importance.GetValueOrDefault(s.SkillId, 1) >= threshold).Select(s => s.SkillId).ToHashSet();
        var queue = new Queue<string>(protectedSkills);
        while (queue.TryDequeue(out var id))
            foreach (var prerequisite in ordered.FirstOrDefault(s => s.SkillId == id)?.PrerequisiteSkillIds ?? [])
                if (protectedSkills.Add(prerequisite)) queue.Enqueue(prerequisite);
        foreach (var mission in plan.Milestones.SelectMany(m => m.Missions).Where(m => !protectedSkills.Contains(m.SkillId) &&
            m.Purpose is "Study" or "WorkedExample" or "FindError" or "Explore" or "Explain" or "CompleteSolution"))
        {
            mission.CanStart = false; mission.Deferred = true; mission.SkipReason = "ExamPhaseLowPriority";
            mission.SelectionReason = "Tạm hoãn kiến thức mới trọng số thấp trong giai đoạn thi thử; giữ các nền tảng cần cho nội dung trọng số cao.";
        }
        var count = (int)Math.Min(4, plan.AvailableMinutesPerDay / 10);
        // Same small-bank trap as the per-skill checks: requiring 5 *unused* questions
        // per skill meant no mock exam was ever generated on a small bank (placement
        // alone consumes the pool). The execution service now serves milestone exams
        // from the tiered pool, so select skills by their total usable questions with
        // the same floor of 3 per skill.
        var selected = ordered.Where(s => questions.Count(q => q.PrimaryTopicNodeId.ToString() == s.SkillId) >= LearningAssessmentPolicy.MinimumPlacementQuestions)
            .Take(count).Select(s => s.SkillId).ToList();
        if (selected.Count == 0) return;
        var mock = new ProposedMissionDto { MissionId = Guid.NewGuid().ToString(), SkillId = selected[0], AssessedSkillIds = selected,
            Title = $"Thi thử bấm giờ · {selected.Count} kỹ năng", Purpose = "IndependentCheck", IsMilestoneExam = true, IsProbe = true,
            QuestionCount = (uint)selected.Count * 5, EstimatedMinutes = (uint)selected.Count * 10, TimeLimitMinutes = (uint)selected.Count * 10,
            ActivityTypes = ["IndependentCheck"], SelectionReason = "Thi thử phạm vi vừa với quỹ học trong ngày, dùng câu mới và không gợi ý. Đây là bài tổng hợp nội bộ, chưa phải đề thi đã hiệu chuẩn. Kết quả sẽ sinh rèn lại theo từng kỹ năng." };
        plan.Milestones.Add(new() { Order = (uint)plan.Milestones.Count + 1, Title = "Thi thử tổng hợp", SkillIds = selected,
            OutcomeDescription = "Làm bài tổng hợp có giới hạn thời gian, sau đó ưu tiên vá lỗ hổng.", Missions = [mock] });
    }

    private async Task<List<ExamMilestoneDto>> ReadClassExamsAsync(Guid center, Guid student, Guid subject, Guid? classId, CancellationToken ct)
    {
        var assignments = await db.Assignments.AsNoTracking().Where(a => a.CenterId == center &&
            a.Class != null && a.Class.SubjectId == subject && (!classId.HasValue || a.ClassId == classId) &&
            a.Kind != AssignmentKind.Regular && a.ExamAt.HasValue &&
            db.ClassStudents.Any(m => m.CenterId == center && m.StudentId == student && m.ClassId == a.ClassId &&
                m.Status == Contracts.Organization.ClassStudentStatus.Active && m.RemovedAt == null && m.Class.Status == Contracts.Organization.ClassStatus.Active && m.Class.LearningScope == Contracts.Organization.ClassLearningScope.Current) &&
            (a.Status == AssignmentStatus.Published || a.Status == AssignmentStatus.Closed) &&
            (a.TargetMode == TargetSource.WholeClass || db.AssignmentTargets.Any(t => t.CenterId == center && t.StudentId == student && t.AssignmentId == a.AssignmentId)))
            .OrderBy(a => a.ExamAt).ToListAsync(ct);
        var progresses = await db.StudentAssignmentProgresses.AsNoTracking().Where(p => p.CenterId == center && p.StudentId == student).ToListAsync(ct);
        var result = new List<ExamMilestoneDto>();
        var allowed = (await LearningPathV4Data.NodesAsync(db, center, student, subject, classId, ct)).Select(n => n.NodeId).ToHashSet();
        foreach (var assignment in assignments)
        {
            var row = progresses.FirstOrDefault(p => p.AssignmentId == assignment.AssignmentId);
            var exam = new ExamMilestoneDto { AssignmentId = assignment.AssignmentId, ClassId = assignment.ClassId,
                Title = assignment.Title, Kind = assignment.Kind.ToString(), ExamAt = assignment.ExamAt!.Value,
                TimeLimitMinutes = assignment.TimeLimitMinutes ?? 0,
                Status = row?.Status == ProgressStatus.Completed ? "PendingGrading" :
                    assignment.Status == AssignmentStatus.Closed && assignment.UpdatedAt < assignment.ExamAt ? "Cancelled" :
                    assignment.Status == AssignmentStatus.Closed || (assignment.DueAt ?? assignment.ExamAt.Value.AddMinutes(assignment.TimeLimitMinutes ?? 0)) < Now ? "Missed" : "Scheduled" };
            if (row?.Status == ProgressStatus.Completed)
            {
                var attempts = await db.Attempts.AsNoTracking().Include(a => a.Question).Where(a => a.CenterId == center && a.StudentId == student &&
                    a.AssignmentId == assignment.AssignmentId).ToListAsync(ct);
                var ids = attempts.Select(a => a.AttemptId).ToList();
                var analyses = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == center && ids.Contains(a.AttemptId)).ToListAsync(ct);
                var settled = attempts.Count > 0 && attempts.All(a => a.Status == AttemptStatus.Completed && a.IsCorrect.HasValue) &&
                    !analyses.Any(a => a.NeedsTeacherReview && a.ReviewDecision == null);
                if (settled)
                {
                    exam.Status = "Completed";
                    var voided = await db.AssignmentQuestions.AsNoTracking().Where(q => q.CenterId == center && q.AssignmentId == assignment.AssignmentId && q.IsVoided)
                        .Select(q => q.QuestionId).ToListAsync(ct);
                    exam.SupportSkillIds = attempts.Where(a => allowed.Contains(a.Question.PrimaryTopicNodeId) && !voided.Contains(a.QuestionId))
                        .GroupBy(a => a.Question.PrimaryTopicNodeId).Where(g => g.Count(a => analyses.FirstOrDefault(r => r.AttemptId == a.AttemptId)?.OverrideIsCorrect ?? a.IsCorrect ?? false) / (decimal)g.Count() < .8m)
                        .Select(g => g.Key.ToString()).ToList();
                }
            }
            result.Add(exam);
        }
        return result;
    }

    private async Task ApplyClassExamFollowUpAsync(Guid center, Guid student, LearningPath path, PathProposalDto plan,
        List<ExamMilestoneDto> exams, Guid? classId, CancellationToken ct)
    {
        foreach (var exam in exams.Where(e => e.Status == "Completed" && e.SupportSkillIds.Count > 0))
        {
            var trigger = $"ClassExam:{exam.AssignmentId:N}:" + string.Join("-", exam.SupportSkillIds.Order());
            if (trigger.Length > 150) trigger = trigger[..65] + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(trigger)))[..20];
            if (await db.PathDecisions.AnyAsync(d => d.CenterId == center && d.LearningPathId == path.LearningPathId && d.TriggerType == trigger, ct)) continue;
            var map = await LearningPathV4Data.MapAsync(db, center, student, path.SubjectId, classId, Now, ct);
            foreach (var skillId in exam.SupportSkillIds)
            {
                var skill = map.Skills.FirstOrDefault(s => s.SkillId == skillId);
                if (skill == null) continue;
                var milestone = plan.Milestones.FirstOrDefault(m => m.SkillIds.Contains(skillId));
                if (milestone == null)
                {
                    milestone = new() { Order = (uint)plan.Milestones.Count + 1, SkillIds = [skillId], Title = skill.SkillName,
                        PrerequisiteSkillIds = skill.PrerequisiteSkillIds, OutcomeDescription = "Củng cố phần cần hỗ trợ sau bài thi của lớp." };
                    plan.Milestones.Add(milestone);
                }
                var existingRepair = milestone.Missions.Where(m => m.IsRepair).Select(m => m.MissionId).ToHashSet();
                if (path.Sessions.Any(s => existingRepair.Contains(s.SessionId.ToString()) && s.Status is not (LearningPathSessionStatus.Completed or LearningPathSessionStatus.Skipped))) continue;
                var pool = await LearningPathV4Data.QuestionsAsync(db, center, student, path.SubjectId, new[] { ulong.Parse(skillId) }, classId, ct);
                var size = Math.Min((uint)pool.Count, Math.Min(6, plan.AvailableMinutesPerDay / 2));
                var repair = new ProposedMissionDto { MissionId = Guid.NewGuid().ToString(), SkillId = skillId,
                    Title = $"Rèn lại sau bài thi: {skill.SkillName}", Purpose = "Practice", IsRepair = true, CanStart = size > 0,
                    ActivityTypes = ["Practice"], QuestionCount = size, EstimatedMinutes = Math.Max(1, size) * 2,
                    SelectionReason = $"Kết quả đã chấm của {exam.Title} cho thấy phần này cần củng cố. Các phần độc lập vẫn tiếp tục học được." };
                milestone.Missions.Insert(0, repair);
                db.LearningPathSessions.Add(new LearningPathSession { SessionId = Guid.Parse(repair.MissionId), CenterId = center,
                    LearningPathId = path.LearningPathId, TopicNodeId = ulong.Parse(skillId), ClassId = classId,
                    Title = repair.Title, Objective = "Practice", Rationale = repair.SelectionReason, PhaseNumber = milestone.Order,
                    SessionOrder = (uint)plan.Milestones.SelectMany(m => m.Missions).Count(), PolicyVersion = LearningPathV4Data.Policy,
                    CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
            }
            db.PathDecisions.Add(new PathDecision { CenterId = center, LearningPathId = path.LearningPathId, TriggerType = trigger,
                DecisionSummary = $"Đã đọc kết quả {exam.Title}; ưu tiên rèn lại {exam.SupportSkillIds.Count} phần cần hỗ trợ, không khóa toàn bộ lộ trình.",
                ReasonCodes = JsonSerializer.SerializeToDocument(new[] { "CLASS_EXAM_TARGETED_REPAIR" }),
                PolicyVersion = LearningPathV4Data.Policy, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
        }
    }
}
