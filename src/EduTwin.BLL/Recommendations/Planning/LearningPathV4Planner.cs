using System.Text.Json;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed class LearningPathV4Planner(EduTwinDbContext db, TimeProvider clock) : ILearningPathV4Planner
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<LearningGoalDto> CreateOrUpdateGoalAsync(Guid centerId, Guid studentId,
        CreateLearningGoalRequest request, CancellationToken cancellationToken = default)
    {
        var nodes = await LearningPathV4Data.NodesAsync(db, centerId, studentId, request.SubjectId, request.ClassId, cancellationToken);
        if (request.AvailableMinutesPerDay is < 5 or > 180 || request.AvailableDaysPerWeek is < 1 or > 7 ||
            request.Title.Length > 200 || request.Description?.Length > 2000 || request.TargetSkillIds.Count > 200 ||
            !new[] { "Foundation", "KeepUp", "Catchup", "ImproveGrade", "ExamPrep", "Advanced", "Diagnostic" }.Contains(request.GoalType))
            throw new ArgumentException("Mục tiêu, phạm vi hoặc thời lượng học không hợp lệ.");
        var skills = request.TargetSkillIds.Distinct().ToList();
        if (skills.Any(id => nodes.All(n => n.NodeId.ToString() != id)))
            throw new ArgumentException("Kỹ năng đã chọn không thuộc môn và giáo trình đang học.");
        if (nodes.Count == 0) throw new InvalidOperationException("Môn học chưa có kỹ năng trong giáo trình để lập lộ trình.");
        var goal = new LearningGoal
        {
            GoalId = Guid.NewGuid(), CenterId = centerId, StudentId = studentId, SubjectId = request.SubjectId,
            ClassId = request.ClassId, GoalType = request.GoalType,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Kế hoạch học tập cá nhân" : request.Title.Trim(),
            Description = request.Description, TargetSkillIds = JsonSerializer.SerializeToDocument(skills.Count > 0 ? skills : nodes.Select(n => n.NodeId.ToString()).ToList()),
            DeadlineDate = request.DeadlineDate, AvailableMinutesPerDay = request.AvailableMinutesPerDay,
            AvailableDaysPerWeek = request.AvailableDaysPerWeek, PreferredPace = request.PreferredPace,
            PreferredMode = request.PreferredMode, Status = "Draft", CreatedAt = Now, UpdatedAt = Now, RowVersion = 1
        };
        // Previewing another goal must not supersede the accepted one.
        db.LearningGoals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return GoalDto(goal);
    }

    public async Task<LearningGoalDto?> GetActiveGoalAsync(Guid centerId, Guid studentId, Guid subjectId, CancellationToken cancellationToken = default)
    {
        var goal = await db.LearningGoals.AsNoTracking().Where(g => g.CenterId == centerId && g.StudentId == studentId &&
            g.SubjectId == subjectId && g.Status == "Active").OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return goal == null ? null : GoalDto(goal);
    }

    public async Task<PathProposalDto> GeneratePathProposalAsync(Guid centerId, Guid studentId, GeneratePathProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var goal = await db.LearningGoals.AsNoTracking().FirstOrDefaultAsync(g => g.CenterId == centerId && g.StudentId == studentId &&
            g.GoalId == request.GoalId && g.SubjectId == request.SubjectId && g.ClassId == request.ClassId, cancellationToken)
            ?? throw new KeyNotFoundException("Mục tiêu không thuộc phạm vi của bạn.");
        var plan = await BuildProposalAsync(centerId, studentId, goal, cancellationToken);
        var proposal = new PathProposal
        {
            ProposalId = plan.ProposalId, CenterId = centerId, StudentId = studentId, GoalId = goal.GoalId,
            SubjectId = goal.SubjectId, StartingBaselineSummary = plan.StartingBaselineSummary,
            CandidatePlan = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json),
            EstimatedWeeks = plan.EstimatedWeeks, FeasibilityStatus = plan.FeasibilityStatus, FeasibilityMessage = plan.FeasibilityMessage,
            ExpiresAt = plan.ExpiresAt, Status = "Pending", CreatedAt = Now, UpdatedAt = Now, RowVersion = 1
        };
        db.PathProposals.Add(proposal);
        await db.SaveChangesAsync(cancellationToken);
        return plan;
    }

    private async Task<PathProposalDto> BuildProposalAsync(Guid center, Guid student, LearningGoal goal, CancellationToken ct)
    {
        var map = await LearningPathV4Data.MapAsync(db, center, student, goal.SubjectId, goal.ClassId, Now, ct);
        var nodes = await LearningPathV4Data.NodesAsync(db, center, student, goal.SubjectId, goal.ClassId, ct);
        var insights = await LearningPathInsights.ReadAsync(db, center, student, goal.SubjectId, goal.ClassId, Now, ct);
        var wanted = (JsonSerializer.Deserialize<List<string>>(goal.TargetSkillIds?.RootElement.GetRawText() ?? "[]") ?? []).ToHashSet();
        // Add prerequisites to the target, without silently expanding outside the permitted curriculum.
        var all = map.Skills.ToDictionary(s => s.SkillId);
        var pending = new Queue<string>(wanted);
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (!all.TryGetValue(id, out var skill)) throw new InvalidOperationException("Mục tiêu đã thay đổi phạm vi; hãy chọn lại kỹ năng.");
            foreach (var p in skill.PrerequisiteSkillIds.Where(all.ContainsKey)) if (wanted.Add(p)) pending.Enqueue(p);
        }
        var selected = map.Skills.Where(s => wanted.Contains(s.SkillId)).ToList();
        var ordered = new List<SkillCapabilityStatusDto>();
        while (selected.Count > 0)
        {
            var ready = selected.Where(s => !s.PrerequisiteSkillIds.Any(p => selected.Any(x => x.SkillId == p)))
                .OrderBy(s => insights.SuggestedSkillIds.Contains(s.SkillId) ? 0 : s.State == "Developing" ? 1 : 2)
                .ThenBy(s => nodes.FindIndex(n => n.NodeId.ToString() == s.SkillId)).FirstOrDefault();
            if (ready == null) throw new InvalidOperationException("Đồ thị kiến thức có vòng lặp; cần giáo viên kiểm tra trước khi lập tuyến.");
            ordered.Add(ready); selected.Remove(ready);
        }
        var questions = await LearningPathV4Data.QuestionsAsync(db, center, goal.SubjectId, nodes.Select(n => n.NodeId).ToList(), ct);
        var content = await db.LearningContentVersions.AsNoTracking().Where(c => c.CenterId == center &&
            c.SubjectId == goal.SubjectId && c.Status == "Published").OrderByDescending(c => c.Version).ToListAsync(ct);
        var usedQuestionIds = await db.Attempts.AsNoTracking().Where(a => a.CenterId == center && a.StudentId == student &&
            a.Question.SubjectId == goal.SubjectId).Select(a => a.QuestionId).Distinct().ToListAsync(ct);
        var milestones = new List<ProposedMilestoneDto>();
        uint order = 0;
        foreach (var skill in ordered)
        {
            var node = nodes.Single(n => n.NodeId.ToString() == skill.SkillId);
            var count = questions.Count(q => q.PrimaryTopicNodeId == node.NodeId);
            var missions = new List<ProposedMissionDto>();
            void Add(string type, string title, uint minutes, bool canStart, string reason, Guid? contentId = null)
                => missions.Add(new ProposedMissionDto
                {
                    MissionId = Guid.NewGuid().ToString(), SkillId = skill.SkillId, Title = title, Purpose = type,
                    ActivityTypes = [type], EstimatedMinutes = minutes, SelectionReason = reason,
                    CanStart = canStart, ContentVersionId = contentId
                });
            if (skill.State != "Confirmed" || goal.GoalType == "Advanced")
            {
                var authored = content.Where(c => LearningPathV4Data.Matches(c, node)).GroupBy(c => c.ActivityType).Select(g => g.First()).ToList();
                foreach (var lesson in authored.Where(c => (skill.State != "Confirmed" || goal.GoalType != "Advanced") && c.ActivityType is "WorkedExample" or "FindError" or "Explore" or "CompleteSolution" or "Explain"))
                    Add(lesson.ActivityType, lesson.Title, lesson.EstimatedDurationMinutes, true,
                        $"Nội dung đã xuất bản cho {node.NodeName}. {skill.Rationale}", lesson.ContentVersionId);
                if (authored.Count == 0 && !(skill.State == "Confirmed" && goal.GoalType == "Advanced"))
                    Add("Study", $"Tìm hiểu: {node.NodeName}", 5, !string.IsNullOrWhiteSpace(node.Description) || count > 0,
                        string.IsNullOrWhiteSpace(node.Description) && count == 0 ? "Chưa có bài học hoặc ví dụ đã xuất bản cho kỹ năng này."
                        : $"Đọc nội dung chủ đề và ví dụ có lời giải sẵn có. {skill.Rationale}");
                var practicePool = LearningPathInsights.PracticePool(questions.Where(q => q.PrimaryTopicNodeId == node.NodeId).ToList(), usedQuestionIds.ToHashSet());
                if (goal.GoalType == "Advanced") practicePool = questions.Where(q => q.PrimaryTopicNodeId == node.NodeId && q.Difficulty >= 4).ToList();
                var practiceCount = Math.Min(practicePool.Count,
                    (int)Math.Clamp(goal.AvailableMinutesPerDay / 2, 5u, 8u));
                var observation = insights.Skills.First(s => s.SkillId == skill.SkillId).Observation;
                Add("Practice", $"Rèn kỹ năng: {node.NodeName}", (uint)Math.Max(1, practiceCount) * 2, practiceCount > 0,
                    practiceCount == 0 ? "Chưa có câu phù hợp; cần giáo viên bổ sung ngân hàng."
                        : $"Chuỗi {practiceCount} câu từ khởi động đến tự luyện, có phản hồi sau từng câu. {observation}"
                          + (practiceCount < 5 ? " Ngân hàng hiện chưa đủ một bộ 5 câu khác nhau." : ""));
                if (count < 10) missions.Last().SelectionReason += " Ngân hàng còn ít câu: sau phần luyện có thể chưa đủ 5 câu mới để xác nhận độc lập; cần bổ sung đề.";
                missions.Last().QuestionCount = (uint)practiceCount;
                if (goal.GoalType == "Advanced")
                {
                    var advanced = missions.Last(); advanced.DifficultyFloor = 4; advanced.Title = $"Luyện nâng cao: {node.NodeName}";
                    advanced.SelectionReason = practiceCount > 0 ? $"{practiceCount} câu khó mức 4–5/5 để vận dụng {node.NodeName}. Không dùng câu cơ bản để thay cho thử thách."
                        : "Chưa có câu khó mức 4–5/5 phù hợp. Cần giáo viên bổ sung; không đổi tên câu cơ bản thành nâng cao.";
                }
            }
            if (!(goal.GoalType == "Advanced" && skill.State == "Confirmed"))
            {
            Add("IndependentCheck", $"Tự kiểm tra: {node.NodeName}", 15, count >= 5,
                count < 5 ? $"Hiện có {count}/5 câu hợp lệ; chưa đủ để xác nhận kỹ năng."
                    : "Kiểm tra bằng câu chưa lộ đáp án; cần ít nhất 5 câu độc lập, đạt 80% và qua kiểm duyệt bằng chứng.");
            missions.Last().QuestionCount = 5;
            if (goal.GoalType is "ExamPrep" or "Diagnostic" or "Advanced" && skill.State != "Confirmed" && skill.Availability != "PrerequisiteBlocked" && missions.Last().CanStart)
            {
                var probe = missions.Last(); missions.RemoveAt(missions.Count - 1);
                probe.Title = $"Kiểm tra điểm xuất phát: {node.NodeName}";
                probe.SelectionReason = "Mục tiêu ôn kiểm tra/nâng cao: kiểm tra phần đã biết trước để tránh luyện lặp. Nếu chưa đạt, quay lại hoạt động học đúng kỹ năng.";
                missions.Insert(0, probe);
            }
            }
            milestones.Add(new ProposedMilestoneDto
            {
                Order = ++order, Title = node.NodeName, OutcomeDescription = node.Description ?? $"Tự thực hiện được các nhiệm vụ về {node.NodeName}.",
                SkillIds = [skill.SkillId], Missions = missions,
                HasTransferChallenge = content.Any(c => LearningPathV4Data.Matches(c, node) && c.ActivityType == "TransferChallenge")
            });
        }
        var minutes = milestones.Sum(m => m.Missions.Sum(a => (int)a.EstimatedMinutes));
        var weeks = (uint)Math.Max(1, Math.Ceiling(minutes / (decimal)(goal.AvailableMinutesPerDay * goal.AvailableDaysPerWeek)));
        var budget = goal.DeadlineDate.HasValue ? Math.Max(0, (goal.DeadlineDate.Value - Now).TotalDays / 7) * goal.AvailableMinutesPerDay * goal.AvailableDaysPerWeek : double.MaxValue;
        var feasible = budget < minutes ? "InsufficientTime" : "Feasible";
        var candidates = milestones.SelectMany(m => m.Missions).Where(m => m.CanStart).ToList();
        return new PathProposalDto
        {
            ProposalId = Guid.NewGuid(), GoalId = goal.GoalId, SubjectId = goal.SubjectId, GoalTitle = goal.Title,
            StartingBaselineSummary = $"{insights.AnswerCount} lượt trả lời trong 30 ngày. {insights.Summary} {ordered.Count} kỹ năng trong mục tiêu và nền tảng liên quan; {ordered.Count(s => s.State == "Confirmed")} có bằng chứng độc lập.",
            ConfirmedSkills = ordered.Where(s => s.State == "Confirmed").Select(s => s.SkillId).ToList(),
            DevelopingSkills = ordered.Where(s => s.State is "Developing" or "Demonstrated").Select(s => s.SkillId).ToList(),
            UnknownSkills = ordered.Where(s => s.State == "Unknown").Select(s => s.SkillId).ToList(),
            Milestones = milestones, InitialMission = candidates.FirstOrDefault(), AlternativeOption = candidates.Skip(1).FirstOrDefault(),
            EstimatedWeeks = weeks, FeasibilityStatus = feasible,
            FeasibilityMessage = $"Ước lượng khoảng {Math.Max(1, weeks - 1)}–{weeks + 1} tuần với lịch {goal.AvailableMinutesPerDay} phút/ngày, {goal.AvailableDaysPerWeek} ngày/tuần; chưa gồm bài bắt buộc và nội dung còn thiếu."
                + (feasible == "InsufficientTime" ? " Thời gian tới hạn chưa đủ; hãy thu hẹp mục tiêu hoặc điều chỉnh lịch, không hạ tiêu chí đánh giá." : ""),
            ExpiresAt = Now.AddDays(1), Status = "Pending"
        };
    }

    public async Task<AcceptPathProposalResponse> AcceptPathProposalAsync(Guid centerId, Guid studentId, Guid proposalId, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await StudentLockHelper.AcquireStudentLockAsync(db, centerId, studentId, cancellationToken);
        var proposal = await db.PathProposals.Include(p => p.Goal).FirstOrDefaultAsync(p => p.CenterId == centerId &&
            p.StudentId == studentId && p.ProposalId == proposalId, cancellationToken) ?? throw new KeyNotFoundException("Đề xuất không thuộc về bạn.");
        var trigger = $"ProposalAccepted:{proposalId:D}";
        var accepted = await db.PathDecisions.AsNoTracking().FirstOrDefaultAsync(d => d.CenterId == centerId && d.TriggerType == trigger, cancellationToken);
        if (accepted != null)
        {
            var prior = await db.LearningPaths.AsNoTracking().SingleAsync(p => p.CenterId == centerId && p.StudentId == studentId && p.LearningPathId == accepted.LearningPathId, cancellationToken);
            return new AcceptPathProposalResponse { LearningPathId = prior.LearningPathId, Version = prior.Version, Message = "Đề xuất này đã được kích hoạt." };
        }
        if (proposal.ExpiresAt <= Now || proposal.Status != "Pending") throw new InvalidOperationException("Đề xuất đã hết hạn hoặc không còn hiệu lực.");
        await LearningPathV4Data.NodesAsync(db, centerId, studentId, proposal.SubjectId, proposal.Goal.ClassId, cancellationToken);
        var openRuns = await db.LearningPathSessionRuns.Include(r => r.Session).Where(r => r.CenterId == centerId && r.StudentId == studentId &&
            r.Session.LearningPath.SubjectId == proposal.SubjectId && r.Status != SessionRunStatus.Completed && r.Status != SessionRunStatus.Abandoned).ToListAsync(cancellationToken);
        var blockers = openRuns.Where(r => r.Session.PolicyVersion == LearningPathV4Data.Policy || r.Status != SessionRunStatus.InProgress)
            .OrderByDescending(r => r.LastActivityAt).Select(r => new BlockingLearningMission(
                r.SessionId, r.SessionRunId, r.Session.Title, r.Status.ToString(), r.Session.PolicyVersion == LearningPathV4Data.Policy)).ToList();
        if (blockers.Count > 0)
            throw new LearningPathChangeBlockedException(blockers);
        // Accepting the preview explicitly migrates unfinished legacy study. Keep every submission;
        // close the old execution as abandoned, never as passed, so it cannot block the new mission player.
        foreach (var legacyRun in openRuns)
        {
            legacyRun.Status = SessionRunStatus.Abandoned; legacyRun.Outcome = SessionRunOutcome.None;
            legacyRun.CompletedAt = Now; legacyRun.UpdatedAt = Now; legacyRun.RowVersion++;
            legacyRun.Session.Status = LearningPathSessionStatus.Skipped; legacyRun.Session.UpdatedAt = Now; legacyRun.Session.RowVersion++;
        }
        var old = await db.LearningPaths.Where(p => p.CenterId == centerId && p.StudentId == studentId && p.SubjectId == proposal.SubjectId).ToListAsync(cancellationToken);
        foreach (var p in old.Where(p => p.Status == LearningPathStatus.Active)) { p.Status = LearningPathStatus.Superseded; p.RowVersion++; p.UpdatedAt = Now; }
        var plan = JsonSerializer.Deserialize<PathProposalDto>(proposal.CandidatePlan.RootElement.GetRawText(), LearningPathV4Data.Json)
            ?? throw new InvalidOperationException("Đề xuất không hợp lệ; hãy tạo lại.");
        var path = new LearningPath
        {
            LearningPathId = Guid.NewGuid(), CenterId = centerId, StudentId = studentId, SubjectId = proposal.SubjectId,
            Version = old.Count == 0 ? 1 : old.Max(p => p.Version) + 1, Status = LearningPathStatus.Active,
            Strategy = LearningPathStrategy.OpportunityGap, PlanSchemaVersion = "4.0", PlanJson = JsonDocument.Parse(proposal.CandidatePlan.RootElement.GetRawText()),
            RecommendationRationale = plan.StartingBaselineSummary, GeneratedAt = Now, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1
        };
        uint order = 0;
        foreach (var milestone in plan.Milestones)
        foreach (var mission in milestone.Missions)
            path.Sessions.Add(new LearningPathSession
            {
                SessionId = Guid.Parse(mission.MissionId), CenterId = centerId, TopicNodeId = ulong.Parse(mission.SkillId),
                ClassId = proposal.Goal.ClassId, Title = mission.Title, Objective = mission.Purpose, Rationale = mission.SelectionReason,
                PolicyVersion = LearningPathV4Data.Policy, MinimumQuestions = mission.Purpose == "IndependentCheck" ? 5u : 1u,
                RequiredCorrectRate = .8m, PhaseNumber = milestone.Order, WeekNumber = milestone.Order, SessionOrder = ++order,
                CreatedAt = Now, UpdatedAt = Now, RowVersion = 1
            });
        db.LearningPaths.Add(path);
        var goals = await db.LearningGoals.Where(g => g.CenterId == centerId && g.StudentId == studentId && g.SubjectId == proposal.SubjectId && g.Status == "Active").ToListAsync(cancellationToken);
        foreach (var g in goals) { g.Status = "Superseded"; g.UpdatedAt = Now; g.RowVersion++; }
        proposal.Goal.Status = "Active"; proposal.Goal.UpdatedAt = Now; proposal.Goal.RowVersion++;
        proposal.Status = "Accepted"; proposal.UpdatedAt = Now; proposal.RowVersion++;
        db.PathDecisions.Add(new PathDecision { CenterId = centerId, LearningPathId = path.LearningPathId,
            TriggerType = trigger, DecisionSummary = plan.StartingBaselineSummary + $" Đã giữ bài làm và kết thúc {openRuns.Count} lượt học cũ theo xác nhận chuyển tuyến.",
            ReasonCodes = JsonSerializer.SerializeToDocument(openRuns.Count > 0 ? new[] { "GOAL_ACCEPTED", "LEGACY_RUNS_CLOSED_ON_ACCEPT" } : new[] { "GOAL_ACCEPTED" }),
            PolicyVersion = LearningPathV4Data.Policy, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
        await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new AcceptPathProposalResponse { LearningPathId = path.LearningPathId, Version = path.Version, FirstMission = plan.InitialMission, Message = "Đã kích hoạt lộ trình theo các kỹ năng bạn chọn." };
    }

    public Task<CapabilityMapDto> GetCapabilityMapAsync(Guid centerId, Guid studentId, Guid subjectId, Guid? classId = null, CancellationToken cancellationToken = default)
        => LearningPathV4Data.MapAsync(db, centerId, studentId, subjectId, classId, Now, cancellationToken);

    public async Task<LearningAgendaDto> GetLearningAgendaAsync(Guid centerId, Guid studentId, Guid subjectId, uint availableMinutes = 15,
        CancellationToken cancellationToken = default, Guid? classId = null)
    {
        var map = await GetCapabilityMapAsync(centerId, studentId, subjectId, classId, cancellationToken);
        var path = await db.LearningPaths.AsNoTracking().Include(p => p.Sessions).Where(p => p.CenterId == centerId && p.StudentId == studentId &&
            p.SubjectId == subjectId && p.PlanSchemaVersion == "4.0" && p.Status == LearningPathStatus.Active).OrderByDescending(p => p.Version).FirstOrDefaultAsync(cancellationToken);
        var result = new LearningAgendaDto { Date = Now, AvailableMinutes = Math.Clamp(availableMinutes, 5u, 180u), LearningPathId = path?.LearningPathId,
            AgendaMessage = path == null ? "Hãy thiết lập mục tiêu và xác nhận đề xuất để bắt đầu tuyến cá nhân. Lộ trình cũ không bị xóa." : "Chọn nhiệm vụ phù hợp. Các chặng là kỹ năng, không phải những tuần lặp lại cùng một chủ đề." };
        result.Insights = await LearningPathInsights.ReadAsync(db, centerId, studentId, subjectId, classId, Now, cancellationToken);
        if (path != null) result.RecentChanges = await db.PathDecisions.AsNoTracking().Where(d => d.CenterId == centerId &&
            d.LearningPathId == path.LearningPathId && d.TriggerType != "MissionEvaluation").OrderByDescending(d => d.CreatedAt)
            .Select(d => d.DecisionSummary).Take(3).ToListAsync(cancellationToken);
        if (path?.PlanJson == null) return result;
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson.RootElement.GetRawText(), LearningPathV4Data.Json);
        if (plan == null) return result;
        result.GoalTitle = plan.GoalTitle;
        var runs = await db.LearningPathSessionRuns.AsNoTracking().Where(r => r.CenterId == centerId && r.StudentId == studentId &&
            r.Session.LearningPathId == path.LearningPathId).OrderByDescending(r => r.RunNumber).ToListAsync(cancellationToken);
        var subjectQuestions = await LearningPathV4Data.QuestionsAsync(db, centerId, subjectId, map.Skills.Select(s => ulong.Parse(s.SkillId)).ToList(), cancellationToken);
        var usedIds = await db.Attempts.AsNoTracking().Where(a => a.CenterId == centerId && a.StudentId == studentId &&
            a.Question.SubjectId == subjectId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
        var exposedIds = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == centerId && i.ActivityRun.SessionRun.StudentId == studentId &&
            i.SolutionExposedAt != null && i.QuestionId != null).Select(i => i.QuestionId!.Value).Distinct().ToListAsync(cancellationToken);
        var options = new List<AgendaItemDto>();
        foreach (var milestone in plan.Milestones)
        foreach (var m in milestone.Missions)
        {
            var session = path.Sessions.FirstOrDefault(s => s.SessionId.ToString() == m.MissionId && s.ClassId == classId);
            var skill = map.Skills.FirstOrDefault(s => s.SkillId == m.SkillId);
            if (session == null || skill == null) continue;
            var active = runs.FirstOrDefault(r => r.SessionId == session.SessionId && r.Status != SessionRunStatus.Completed && r.Status != SessionRunStatus.Abandoned);
            var item = new AgendaItemDto
            {
                QuestionCount = m.QuestionCount, AvailableFrom = m.AvailableFrom,
                MissionId = m.MissionId, Title = m.Title, Objective = milestone.OutcomeDescription, Purpose = m.Purpose,
                SkillId = m.SkillId, SkillName = skill.SkillName, EstimatedDurationMinutes = m.EstimatedMinutes, ActivityTypes = m.ActivityTypes,
                RecommendationReason = $"{m.SelectionReason} {skill.Rationale}", Status = session.Status.ToString(), ActiveRunId = active?.SessionRunId,
                LatestRunId = runs.FirstOrDefault(r => r.SessionId == session.SessionId)?.SessionRunId,
                CanStart = m.CanStart && (!m.AvailableFrom.HasValue || m.AvailableFrom <= Now) && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped &&
                    (m.Purpose != "IndependentCheck" || skill.Availability != "PrerequisiteBlocked")
            };
            if (m.Purpose == "IndependentCheck" && active == null)
            {
                var fresh = subjectQuestions.Count(q => q.PrimaryTopicNodeId.ToString() == m.SkillId && !usedIds.Contains(q.QuestionId) && !exposedIds.Contains(q.QuestionId));
                item.CanStart = fresh >= 5 && skill.Availability != "PrerequisiteBlocked" && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped;
                if (fresh < 5) { item.CanStart = false; item.RecommendationReason = $"Còn {fresh}/5 câu mới đủ điều kiện kiểm tra; cần giáo viên bổ sung câu, không dùng lại câu đã lộ đáp án."; }
                else item.RecommendationReason = $"Có {fresh} câu mới đủ điều kiện; sẽ chọn 5 câu để tự kiểm tra. Lời giải và nhận xét chỉ mở sau khi nộp cả bộ.";
            }
            if (m.Purpose is "Practice" or "Review")
            {
                item.QuestionCount = (uint)Math.Min(m.QuestionCount == 0 ? 6 : m.QuestionCount,
                    m.DifficultyFloor.HasValue ? subjectQuestions.Count(q => q.PrimaryTopicNodeId.ToString() == m.SkillId && q.Difficulty >= m.DifficultyFloor)
                        : LearningPathInsights.PracticePool(subjectQuestions.Where(q => q.PrimaryTopicNodeId.ToString() == m.SkillId).ToList(), usedIds.ToHashSet()).Count);
                item.CanStart = item.QuestionCount > 0 && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped && (!m.AvailableFrom.HasValue || m.AvailableFrom <= Now);
                item.EstimatedDurationMinutes = Math.Max(1u, item.QuestionCount) * 2;
            }
            result.Missions.Add(item);
            if (m.Purpose == "Review" && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped) result.ReviewDueTasks.Add(item);
            if (active != null) { result.OngoingRuns.Add(item); continue; }
            if (session.Status == LearningPathSessionStatus.Completed || !item.CanStart) continue;
            // New confirmed evidence (including assignments) skips redundant future guided work.
            if (skill.State == "Confirmed" && m.Purpose != "Review" && !m.DifficultyFloor.HasValue) continue;
            options.Add(item);
        }
        options = options.OrderBy(m => m.Purpose == "Review" || m.Title.StartsWith("Rèn lại") ? 0 : 1).ToList();
        result.PrimaryMission = result.OngoingRuns.FirstOrDefault() ?? options.FirstOrDefault(m => m.EstimatedDurationMinutes <= result.AvailableMinutes);
        result.AlternativeOptions = options.Where(m => m.MissionId != result.PrimaryMission?.MissionId && m.EstimatedDurationMinutes <= result.AvailableMinutes).Take(2).ToList();
        if (result.PrimaryMission == null) result.AgendaMessage = options.Count > 0
            ? "Chưa có nhiệm vụ đủ ngắn cho thời gian này. Hãy tăng thời gian hoặc chọn một hoạt động trong tuyến; bạn có thể lưu và tiếp tục."
            : "Các hoạt động khả dụng đã hoàn thành hoặc còn thiếu nội dung/bằng chứng. Xem từng chặng để biết phần cần bổ sung.";
        return result;
    }

    private static LearningGoalDto GoalDto(LearningGoal g) => new()
    {
        GoalId = g.GoalId, StudentId = g.StudentId, SubjectId = g.SubjectId, ClassId = g.ClassId, Title = g.Title, GoalType = g.GoalType,
        Description = g.Description, TargetSkillIds = JsonSerializer.Deserialize<List<string>>(g.TargetSkillIds?.RootElement.GetRawText() ?? "[]") ?? [],
        DeadlineDate = g.DeadlineDate, AvailableMinutesPerDay = g.AvailableMinutesPerDay, AvailableDaysPerWeek = g.AvailableDaysPerWeek,
        PreferredMode = g.PreferredMode, PreferredPace = g.PreferredPace, Status = g.Status, CreatedAt = g.CreatedAt, UpdatedAt = g.UpdatedAt
    };
}
