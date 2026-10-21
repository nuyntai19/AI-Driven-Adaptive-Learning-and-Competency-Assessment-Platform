using System.Text.Json;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed partial class LearningPathV4Planner(EduTwinDbContext db, TimeProvider clock) : ILearningPathV4Planner
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<LearningGoalDto> CreateOrUpdateGoalAsync(Guid centerId, Guid studentId,
        CreateLearningGoalRequest request, CancellationToken cancellationToken = default)
    {
        var nodes = await LearningPathV4Data.NodesAsync(db, centerId, studentId, request.SubjectId, request.ClassId, cancellationToken);
        if (request.AvailableMinutesPerDay is < 5 or > 300 || request.AvailableDaysPerWeek is < 1 or > 7 ||
            request.Title.Length > 200 || request.Description?.Length > 2000 || request.TargetSkillIds.Count > 200 || request.SelfReportedPrerequisiteSkillIds is null || request.SelfReportedPrerequisiteSkillIds.Count > 200 || request.SelfAssessedSkillIds is not { Count: <= 200 } ||
            !new[] { "Foundation", "KeepUp", "Catchup", "ImproveGrade", "ExamPrep", "Advanced", "Diagnostic", "Placement" }.Contains(request.GoalType))
            throw new ArgumentException("Mục tiêu, phạm vi hoặc thời lượng học không hợp lệ.");
        var skills = request.TargetSkillIds.Distinct().ToList();
        if (skills.Any(id => nodes.All(n => n.NodeId.ToString() != id)))
            throw new ArgumentException("Kỹ năng đã chọn không thuộc môn và giáo trình đang học.");
        if (nodes.Count == 0) throw new InvalidOperationException("Môn học chưa có kỹ năng trong giáo trình để lập lộ trình.");
        var scope = new LearningGoalScope { TargetSkillIds = skills.Count > 0 ? skills : nodes.Select(n => n.NodeId.ToString()).ToList(),
            SelfReportedPrerequisiteSkillIds = request.SelfReportedPrerequisiteSkillIds.Distinct().ToList(),
            // Placement leaves out parts the student says are already mastered; they are only meaningful for a learning route.
            SelfAssessedSkillIds = request.GoalType == "Placement" ? [] : request.SelfAssessedSkillIds.Distinct().ToList() };
        if (scope.SelfAssessedSkillIds.Any(id => nodes.All(n => n.NodeId.ToString() != id) || scope.TargetSkillIds.Contains(id) || scope.SelfReportedPrerequisiteSkillIds.Contains(id)))
            throw new ArgumentException("Phần tự đánh giá đã vững phải thuộc giáo trình và nằm ngoài các phần đã chọn học.");
        if (scope.SelfReportedPrerequisiteSkillIds.Count > 0)
        {
            var map = await GetCapabilityMapAsync(centerId, studentId, request.SubjectId, request.ClassId, cancellationToken);
            var byId = map.Skills.ToDictionary(s => s.SkillId);
            var visited = new HashSet<string>(scope.TargetSkillIds);
            var queue = new Queue<string>(scope.TargetSkillIds);
            while (queue.TryDequeue(out var id))
                if (byId.TryGetValue(id, out var skill))
                    foreach (var prerequisite in skill.PrerequisiteSkillIds)
                        if (visited.Add(prerequisite)) queue.Enqueue(prerequisite);
            if (scope.SelfReportedPrerequisiteSkillIds.Any(id => !byId.ContainsKey(id) || !visited.Contains(id) || scope.TargetSkillIds.Contains(id)))
                throw new ArgumentException("Chỉ tự xác nhận nền tảng liên quan trong giáo trình và ngoài các phần đã chọn học.");
        }
        var goal = new LearningGoal
        {
            GoalId = Guid.NewGuid(), CenterId = centerId, StudentId = studentId, SubjectId = request.SubjectId,
            ClassId = request.ClassId, GoalType = request.GoalType,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Kế hoạch học tập cá nhân" : request.Title.Trim(),
            Description = request.Description, TargetSkillIds = scope.Store(),
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
        var scope = LearningGoalScope.Read(goal.TargetSkillIds);
        var wanted = scope.TargetSkillIds.ToHashSet();
        // Add prerequisites to the target, without silently expanding outside the permitted curriculum.
        var all = map.Skills.ToDictionary(s => s.SkillId);
        var pending = new Queue<string>(wanted);
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (!all.TryGetValue(id, out var skill)) throw new InvalidOperationException("Mục tiêu đã thay đổi phạm vi; hãy chọn lại kỹ năng.");
            foreach (var p in skill.PrerequisiteSkillIds.Where(p => all.ContainsKey(p) && !scope.SelfReportedPrerequisiteSkillIds.Contains(p) && !scope.SelfAssessedSkillIds.Contains(p)))
                if (wanted.Add(p)) pending.Enqueue(p);
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
        var exposedQuestionIds = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == center &&
            i.ActivityRun.SessionRun.StudentId == student && i.SolutionExposedAt != null && i.QuestionId != null)
            .Select(i => i.QuestionId!.Value).Distinct().ToListAsync(ct);
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
            if (goal.GoalType == "Placement")
            {
                if (skill.State != "Confirmed")
                {
                    var fresh = questions.Count(q => q.PrimaryTopicNodeId == node.NodeId &&
                        !usedQuestionIds.Contains(q.QuestionId) && !exposedQuestionIds.Contains(q.QuestionId));
                    Add("IndependentCheck", $"Kiểm tra đầu vào: {node.NodeName}", 10, fresh >= 5,
                        fresh >= 5 ? "5 câu mới, từ cơ bản đến vận dụng. Không mở gợi ý hoặc lời giải trước khi nộp cả phần; kết quả dùng để lập tuyến học."
                            : $"Còn {fresh}/5 câu mới; cần giáo viên bổ sung. Có thể lập tuyến tạm từ các phần đã kiểm tra, phần này vẫn ghi chưa đủ bằng chứng.");
                    missions.Last().QuestionCount = 5;
                    missions.Last().IsProbe = true;
                }
            }
            if (goal.GoalType != "Placement" && (skill.State != "Confirmed" || goal.GoalType == "Advanced"))
            {
                var authored = content.Where(c => LearningPathV4Data.Matches(c, node)).GroupBy(c => c.ActivityType).Select(g => g.First()).ToList();
                foreach (var lesson in authored.Where(c => (skill.State != "Confirmed" || goal.GoalType != "Advanced") && c.ActivityType is "WorkedExample" or "FindError" or "Explore" or "CompleteSolution" or "Explain"))
                    Add(lesson.ActivityType, lesson.Title, lesson.EstimatedDurationMinutes, true,
                        $"Nội dung đã xuất bản cho {node.NodeName}. {skill.Rationale}", lesson.ContentVersionId);
                var insight = insights.Skills.FirstOrDefault(s => s.SkillId == skill.SkillId);
                var weakness = LearningPathPacing.Weakness(skill.State, insight);
                var level = LearningPathPacing.Level(weakness);
                var levelLabel = LearningPathPacing.LevelLabel(weakness);
                var practicePool = LearningPathInsights.PracticePool(questions.Where(q => q.PrimaryTopicNodeId == node.NodeId).ToList(), usedQuestionIds.ToHashSet());
                if (goal.GoalType == "Advanced") practicePool = questions.Where(q => q.PrimaryTopicNodeId == node.NodeId && q.Difficulty >= 4).ToList();
                var estimatedSets = LearningPathPacing.EstimatedSetsFor(weakness);
                // No answers yet: try a few questions first instead of making a student who may already know it read theory.
                var probeFirst = insight is not { QuestionCount: > 0 } && skill.State == "Unknown" && goal.GoalType is not ("Foundation" or "Advanced") && practicePool.Count >= 3;
                // Strong recent answers skip re-reading theory (it stays available from review); weak skills restart from the concept.
                var skipStudy = level == "Strong" && insight is { QuestionCount: >= 3 } && goal.GoalType != "Foundation";
                if (probeFirst)
                {
                    var probeSize = (int)Math.Min(LearningPathPacing.ProbeSize, (uint)practicePool.Count);
                    Add("Practice", $"Thử nhanh: {node.NodeName}", (uint)probeSize * LearningPathPacing.MinutesPerQuestion, true,
                        $"Chưa có bài làm cho kỹ năng này: thử {probeSize} câu trước. Làm tốt sẽ bỏ qua phần lý thuyết và lên bộ khó hơn.");
                    var probe = missions.Last(); probe.QuestionCount = (uint)probeSize; probe.TargetDifficulty = 2; probe.SetNumber = 1; probe.IsProbe = true; probe.EstimatedSets = estimatedSets;
                }
                if (authored.Count == 0 && !(skill.State == "Confirmed" && goal.GoalType == "Advanced") && !skipStudy)
                    Add("Study", $"Tìm hiểu: {node.NodeName}", level == "Weak" ? 10u : 5u,
                        StudyMaterial.Available(node.Description, questions.Where(q => q.PrimaryTopicNodeId == node.NodeId).ToList(), usedQuestionIds),
                        !StudyMaterial.Available(node.Description, questions.Where(q => q.PrimaryTopicNodeId == node.NodeId).ToList(), usedQuestionIds) ? StudyMaterial.MissingMessage
                        : probeFirst ? "Ý chính và ví dụ mẫu từng bước. Sẽ được bỏ qua nếu phần thử nhanh đạt từ 80%."
                        : $"{levelLabel}: ý chính và ví dụ mẫu từng bước trước khi luyện.");
                if (!probeFirst)
                {
                    var size = (int)Math.Min(LearningPathPacing.SetSize, (uint)practicePool.Count);
                    Add("Practice", $"Rèn kỹ năng: {node.NodeName} · bộ 1", (uint)Math.Max(1, size) * LearningPathPacing.MinutesPerQuestion, size > 0,
                        size == 0 ? "Chưa có câu phù hợp; cần giáo viên bổ sung ngân hàng."
                            : $"{levelLabel}: luyện theo vòng, mỗi bộ {LearningPathPacing.SetSize} câu, dự kiến {estimatedSets} bộ. Sau mỗi bộ hệ thống chọn bộ tiếp theo theo kết quả.");
                    var first = missions.Last(); first.QuestionCount = (uint)size; first.SetNumber = 1; first.EstimatedSets = estimatedSets;
                    first.TargetDifficulty = LearningPathPacing.StartDifficulty(weakness);
                }
                if (count < 10) missions.Last().SelectionReason += " Ngân hàng còn ít câu nên vòng luyện sẽ ngắn; cần giáo viên bổ sung đề.";
                if (goal.GoalType == "Advanced")
                {
                    var advanced = missions.Last(); advanced.DifficultyFloor = 4; advanced.TargetDifficulty = 4; advanced.Title = $"Luyện nâng cao: {node.NodeName} · bộ 1";
                    advanced.SelectionReason = practicePool.Count > 0 ? $"Câu khó mức 4–5/5 để vận dụng {node.NodeName}, mỗi bộ {LearningPathPacing.SetSize} câu."
                        : "Chưa có câu khó mức 4–5/5 phù hợp. Cần giáo viên bổ sung; không đổi tên câu cơ bản thành nâng cao.";
                }
            }
            if (goal.GoalType != "Placement" && skill.State == "Confirmed" && goal.GoalType != "Advanced")
            {
                var reviewCount = Math.Min(5u, (uint)count);
                Add("Review", $"Ôn nhớ lại: {node.NodeName}", reviewCount * LearningPathPacing.MinutesPerQuestion,
                    reviewCount > 0, "Đã có bằng chứng hợp lệ; bỏ luyện cơ bản và hẹn ôn để duy trì kiến thức.");
                missions.Last().QuestionCount = reviewCount;
                missions.Last().AvailableFrom = Now.AddDays(2);
            }
            else if (goal.GoalType != "Placement" && !(goal.GoalType == "Advanced" && skill.State == "Confirmed"))
            {
            Add("IndependentCheck", $"Tự kiểm tra: {node.NodeName}", 15, count >= 5,
                count < 5 ? $"Hiện có {count}/5 câu hợp lệ; chưa đủ để xác nhận kỹ năng."
                    : "Kiểm tra bằng câu chưa lộ đáp án; cần ít nhất 5 câu độc lập, đạt 80% và qua kiểm duyệt bằng chứng.");
            missions.Last().QuestionCount = 5;
            if (goal.GoalType is "ExamPrep" or "Diagnostic" or "Advanced" && skill.State != "Confirmed" && LearningGoalScope.PrerequisitesSatisfied(skill, map, LearningGoalScope.Vouched(scope.SelfReportedPrerequisiteSkillIds, scope.SelfAssessedSkillIds)) && missions.Last().CanStart)
            {
                var probe = missions.Last(); missions.RemoveAt(missions.Count - 1);
                probe.Title = $"Kiểm tra điểm xuất phát: {node.NodeName}";
                probe.IsProbe = true;
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
        // Option (b): each part left out of the placement check gets one short review at the end of the route.
        // Passing schedules spaced recall; a low score adds a repair set and a self-check (see LearningPathFollowUp).
        if (goal.GoalType != "Placement")
            foreach (var node in nodes.Where(n => scope.SelfAssessedSkillIds.Contains(n.NodeId.ToString()) && !wanted.Contains(n.NodeId.ToString())))
            {
                var skill = map.Skills.FirstOrDefault(s => s.SkillId == node.NodeId.ToString());
                if (skill == null || skill.State == "Confirmed") continue;
                var pool = LearningPathInsights.PracticePool(questions.Where(q => q.PrimaryTopicNodeId == node.NodeId).ToList(), usedQuestionIds.ToHashSet());
                var size = (uint)Math.Min(LearningPathPacing.ConfirmationSize, pool.Count);
                milestones.Add(new ProposedMilestoneDto
                {
                    Order = ++order, Title = node.NodeName, IsSelfAssessedCheck = true, SkillIds = [skill.SkillId],
                    OutcomeDescription = $"Em tự đánh giá đã vững {node.NodeName}. Một bài ôn ngắn ở cuối lộ trình để xác nhận.",
                    Missions =
                    [
                        new ProposedMissionDto
                        {
                            MissionId = Guid.NewGuid().ToString(), SkillId = skill.SkillId, Purpose = "Review", ActivityTypes = ["Practice"],
                            Title = $"Ôn xác nhận: {node.NodeName}", QuestionCount = size, IsSelfAssessedCheck = true,
                            EstimatedMinutes = Math.Max(1u, size) * LearningPathPacing.MinutesPerQuestion, CanStart = size > 0,
                            SelectionReason = size == 0 ? "Chưa có câu phù hợp để ôn xác nhận; cần giáo viên bổ sung ngân hàng câu hỏi."
                                : $"Em đã bỏ qua phần này khi kiểm tra đầu vào vì tự đánh giá đã vững. {size} câu ngắn để xác nhận; đạt thì giữ lịch ôn, chưa đạt sẽ thêm bộ rèn lại."
                        }
                    ]
                });
            }
        // Later sets are created by the practice loop; include them in the time estimate.
        var minutes = milestones.Sum(m => m.Missions.Sum(a => (int)a.EstimatedMinutes +
            (a.Purpose == "Practice" && a.EstimatedSets > 1 ? (int)((a.EstimatedSets - 1) * LearningPathPacing.SetSize * LearningPathPacing.MinutesPerQuestion) : 0)));
        var weeks = (uint)Math.Max(1, Math.Ceiling(minutes / (decimal)(goal.AvailableMinutesPerDay * goal.AvailableDaysPerWeek)));
        var budget = goal.DeadlineDate.HasValue ? Math.Max(0, (goal.DeadlineDate.Value - Now).TotalDays / 7) * goal.AvailableMinutesPerDay * goal.AvailableDaysPerWeek : double.MaxValue;
        var feasible = budget < minutes ? "InsufficientTime" : "Feasible";
        var candidates = milestones.SelectMany(m => m.Missions).Where(m => m.CanStart && (!m.AvailableFrom.HasValue || m.AvailableFrom <= Now)).ToList();
        return new PathProposalDto
        {
            IsPlacement = goal.GoalType == "Placement", GoalType = goal.GoalType,
            RequestedSkillIds = scope.TargetSkillIds, SelfReportedPrerequisiteSkillIds = scope.SelfReportedPrerequisiteSkillIds,
            SelfAssessedSkillIds = milestones.Where(m => m.IsSelfAssessedCheck).SelectMany(m => m.SkillIds).ToList(),
            AvailableMinutesPerDay = goal.AvailableMinutesPerDay, AvailableDaysPerWeek = goal.AvailableDaysPerWeek, DeadlineDate = goal.DeadlineDate,
            PacingVersion = 2, ProposalId = Guid.NewGuid(), GoalId = goal.GoalId, SubjectId = goal.SubjectId, GoalTitle = goal.Title,
            StartingBaselineSummary = $"{insights.AnswerCount} lượt trả lời trong 30 ngày. {insights.Summary} {ordered.Count} kỹ năng trong tuyến; {ordered.Count(s => s.State == "Confirmed")} có bằng chứng độc lập."
                + (scope.SelfReportedPrerequisiteSkillIds.Count > 0 ? $" Đã bỏ qua {scope.SelfReportedPrerequisiteSkillIds.Count} nền tảng theo tự xác nhận của em; chưa coi đó là bằng chứng nắm vững trên hệ thống." : "")
                + (milestones.Any(m => m.IsSelfAssessedCheck) ? $" {milestones.Count(m => m.IsSelfAssessedCheck)} phần em tự đánh giá đã vững có bài ôn xác nhận ngắn ở cuối lộ trình." : ""),
            ConfirmedSkills = ordered.Where(s => s.State == "Confirmed").Select(s => s.SkillId).ToList(),
            DevelopingSkills = ordered.Where(s => s.State is "Developing" or "Demonstrated").Select(s => s.SkillId).ToList(),
            UnknownSkills = ordered.Where(s => s.State == "Unknown").Select(s => s.SkillId).ToList(),
            Milestones = milestones, InitialMission = candidates.FirstOrDefault(), AlternativeOption = candidates.Skip(1).FirstOrDefault(),
            EstimatedWeeks = weeks, FeasibilityStatus = feasible,
            FeasibilityMessage = goal.GoalType == "Placement"
                ? $"Kiểm tra theo từng phần, khoảng {minutes} phút tổng cộng; có thể lưu và tiếp tục. Nền tảng được thêm hoặc bỏ qua theo lựa chọn đã xác nhận. Phần đã có bằng chứng hợp lệ được dùng lại; lịch luyện sẽ đề xuất sau kết quả."
                : $"Ước lượng khoảng {Math.Max(1, weeks - 1)}–{weeks + 1} tuần với lịch {goal.AvailableMinutesPerDay} phút/ngày, {goal.AvailableDaysPerWeek} ngày/tuần; chưa gồm bài bắt buộc và nội dung còn thiếu."
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
        return new AcceptPathProposalResponse { LearningPathId = path.LearningPathId, Version = path.Version, FirstMission = plan.InitialMission,
            Message = plan.IsPlacement ? "Đã lưu phạm vi kiểm tra đầu vào. Em có thể làm lần lượt từng phần." : "Đã kích hoạt lộ trình theo các kỹ năng bạn chọn." };
    }

    public Task<CapabilityMapDto> GetCapabilityMapAsync(Guid centerId, Guid studentId, Guid subjectId, Guid? classId = null, CancellationToken cancellationToken = default)
        => LearningPathV4Data.MapAsync(db, centerId, studentId, subjectId, classId, Now, cancellationToken);

    public async Task<LearningAgendaDto> GetLearningAgendaAsync(Guid centerId, Guid studentId, Guid subjectId, uint availableMinutes = 15,
        CancellationToken cancellationToken = default, Guid? classId = null)
    {
        var map = await GetCapabilityMapAsync(centerId, studentId, subjectId, classId, cancellationToken);
        var path = await db.LearningPaths.AsNoTracking().Include(p => p.Sessions).Where(p => p.CenterId == centerId && p.StudentId == studentId &&
            p.SubjectId == subjectId && p.PlanSchemaVersion == "4.0" && p.Status == LearningPathStatus.Active).OrderByDescending(p => p.Version).FirstOrDefaultAsync(cancellationToken);
        var result = new LearningAgendaDto { Date = Now, AvailableMinutes = LearningPathPacing.ClampMinutes(availableMinutes), LearningPathId = path?.LearningPathId,
            AgendaMessage = path == null ? "Chọn phần muốn học và kiểm tra đầu vào, hoặc bắt đầu từ nền tảng. EduTwin sẽ đề xuất tuyến từ kết quả của em." : "Kế hoạch hôm nay được xếp theo thời gian bạn chọn: phần yếu được ưu tiên và nhận nhiều câu hơn." };
        result.Insights = await LearningPathInsights.ReadAsync(db, centerId, studentId, subjectId, classId, Now, cancellationToken);
        result.PathHistory = await PathHistoryAsync(centerId, studentId, subjectId, path?.LearningPathId, cancellationToken);
        if (path != null) result.RecentChanges = await db.PathDecisions.AsNoTracking().Where(d => d.CenterId == centerId &&
            d.LearningPathId == path.LearningPathId && d.TriggerType != "MissionEvaluation").OrderByDescending(d => d.CreatedAt)
            .Select(d => d.DecisionSummary).Take(3).ToListAsync(cancellationToken);
        if (path?.PlanJson == null) return result;
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson.RootElement.GetRawText(), LearningPathV4Data.Json);
        if (plan == null) return result;
        var activeGoal = await db.LearningGoals.AsNoTracking().FirstOrDefaultAsync(g => g.CenterId == centerId &&
            g.StudentId == studentId && g.GoalId == plan.GoalId, cancellationToken);
        result.GoalTitle = plan.GoalTitle;
        result.IsPlacement = plan.IsPlacement;
        result.GoalType = activeGoal?.GoalType ?? plan.GoalType;
        result.DeadlineDate = activeGoal?.DeadlineDate ?? plan.DeadlineDate;
        result.SelfReportedPrerequisiteSkillIds = plan.SelfReportedPrerequisiteSkillIds;
        result.SelfAssessedSkillIds = plan.SelfAssessedSkillIds;
        var vouched = LearningGoalScope.Vouched(plan.SelfReportedPrerequisiteSkillIds, plan.SelfAssessedSkillIds);
        result.TargetSkillIds = activeGoal?.TargetSkillIds == null
            ? plan.RequestedSkillIds.Count > 0 ? plan.RequestedSkillIds : plan.Milestones.SelectMany(m => m.SkillIds).Distinct().ToList()
            : LearningGoalScope.Read(activeGoal.TargetSkillIds).TargetSkillIds;
        result.AvailableMinutesPerDay = activeGoal?.AvailableMinutesPerDay ?? plan.AvailableMinutesPerDay;
        result.AvailableDaysPerWeek = activeGoal?.AvailableDaysPerWeek ?? plan.AvailableDaysPerWeek;
        var runs = await db.LearningPathSessionRuns.AsNoTracking().Where(r => r.CenterId == centerId && r.StudentId == studentId &&
            r.Session.LearningPathId == path.LearningPathId).OrderByDescending(r => r.RunNumber).ToListAsync(cancellationToken);
        var runIds = runs.Select(r => r.SessionRunId).ToList();
        var runItems = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == centerId &&
            runIds.Contains(i.ActivityRun.SessionRunId) && i.QuestionId != null && i.SolutionExposedAt == null)
            .Select(i => new { i.ActivityRun.SessionRunId, Answered = i.AttemptId != null, Correct = i.Attempt != null && i.Attempt.IsCorrect == true })
            .ToListAsync(cancellationToken);
        var subjectQuestions = await LearningPathV4Data.QuestionsAsync(db, centerId, subjectId, map.Skills.Select(s => ulong.Parse(s.SkillId)).ToList(), cancellationToken);
        var usedIds = await db.Attempts.AsNoTracking().Where(a => a.CenterId == centerId && a.StudentId == studentId &&
            a.Question.SubjectId == subjectId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
        var exposedIds = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == centerId && i.ActivityRun.SessionRun.StudentId == studentId &&
            i.SolutionExposedAt != null && i.QuestionId != null).Select(i => i.QuestionId!.Value).Distinct().ToListAsync(cancellationToken);
        var options = new List<AgendaItemDto>();
        var finalChecks = new List<AgendaItemDto>();
        var poolSizes = new Dictionary<string, int>();
        var weaknessBySkill = new Dictionary<string, decimal>();
        foreach (var milestone in plan.Milestones)
        foreach (var m in milestone.Missions)
        {
            var session = path.Sessions.FirstOrDefault(s => s.SessionId.ToString() == m.MissionId && s.ClassId == classId);
            var skill = map.Skills.FirstOrDefault(s => s.SkillId == m.SkillId);
            if (session == null || skill == null) continue;
            var weakness = LearningPathPacing.Weakness(skill.State, result.Insights.Skills.FirstOrDefault(s => s.SkillId == m.SkillId));
            weaknessBySkill[m.SkillId] = weakness;
            var sessionRuns = runs.Where(r => r.SessionId == session.SessionId).ToList();
            var active = sessionRuns.FirstOrDefault(r => r.Status != SessionRunStatus.Completed && r.Status != SessionRunStatus.Abandoned);
            // A cancelled run is history, not the mission's result: the mission can start again.
            var latest = sessionRuns.FirstOrDefault(r => r.Status != SessionRunStatus.Abandoned);
            var item = new AgendaItemDto
            {
                QuestionCount = m.QuestionCount, DefaultQuestionCount = m.QuestionCount, AvailableFrom = m.AvailableFrom,
                MissionId = m.MissionId, Title = m.Title, Objective = milestone.OutcomeDescription, Purpose = m.Purpose,
                SkillId = m.SkillId, SkillName = skill.SkillName, EstimatedDurationMinutes = m.EstimatedMinutes, ActivityTypes = m.ActivityTypes,
                // The route text may already quote the skill rationale; never repeat it.
                RecommendationReason = string.IsNullOrWhiteSpace(skill.Rationale) || m.SelectionReason.Contains(skill.Rationale) ? m.SelectionReason : $"{m.SelectionReason} {skill.Rationale}",
                Status = session.Status.ToString(), ActiveRunId = active?.SessionRunId,
                LatestRunId = latest?.SessionRunId, IsSelfAssessedCheck = m.IsSelfAssessedCheck, WeaknessLevel = m.IsSelfAssessedCheck ? null : LearningPathPacing.Level(weakness), SetNumber = m.SetNumber, EstimatedSets = m.EstimatedSets,
                CanStart = m.CanStart && (!m.AvailableFrom.HasValue || m.AvailableFrom <= Now) && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped &&
                    (plan.IsPlacement || m.Purpose != "IndependentCheck" || LearningGoalScope.PrerequisitesSatisfied(skill, map, vouched)),
                Runs = sessionRuns.OrderBy(r => r.RunNumber).Select(r => new MissionRunSummaryDto
                {
                    SessionRunId = r.SessionRunId, RunNumber = r.RunNumber, Status = r.Status.ToString(), Outcome = r.Outcome.ToString(),
                    StartedAt = r.StartedAt, CompletedAt = r.CompletedAt,
                    TotalQuestions = (uint)runItems.Count(i => i.SessionRunId == r.SessionRunId),
                    AnsweredQuestions = (uint)runItems.Count(i => i.SessionRunId == r.SessionRunId && i.Answered),
                    CorrectAnswers = (uint)runItems.Count(i => i.SessionRunId == r.SessionRunId && i.Correct)
                }).ToList()
            };
            var fresh = subjectQuestions.Count(q => q.PrimaryTopicNodeId.ToString() == m.SkillId && !usedIds.Contains(q.QuestionId) && !exposedIds.Contains(q.QuestionId));
            if (m.Purpose == "Study" && !m.ContentVersionId.HasValue && active == null && latest == null)
            {
                var node = await db.KnowledgeNodes.AsNoTracking().SingleAsync(n => n.CenterId == centerId && n.NodeId == ulong.Parse(m.SkillId), cancellationToken);
                item.CanStart = StudyMaterial.Available(node.Description, subjectQuestions.Where(q => q.PrimaryTopicNodeId == node.NodeId).ToList(), usedIds)
                    && session.Status != LearningPathSessionStatus.Skipped;
                item.RecommendationReason = item.CanStart
                    ? $"Ôn lại nội dung và ví dụ có lời giải của {node.NodeName}; những câu đã làm có thể dùng lại để học mà không lộ câu tự kiểm tra mới."
                    : StudyMaterial.MissingMessage;
            }
            if (m.Purpose == "IndependentCheck" && active == null)
            {
                item.CanStart = fresh >= 5 && (plan.IsPlacement || LearningGoalScope.PrerequisitesSatisfied(skill, map, vouched)) && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped &&
                    !(plan.IsPlacement && latest?.Status == SessionRunStatus.Completed);
                if (fresh < 5) { item.CanStart = false; item.RecommendationReason = $"Còn {fresh}/5 câu mới đủ điều kiện kiểm tra; cần giáo viên bổ sung câu, không dùng lại câu đã lộ đáp án."; }
                else item.RecommendationReason = $"Có {fresh} câu mới đủ điều kiện; sẽ chọn 5 câu để tự kiểm tra. Lời giải và nhận xét chỉ mở sau khi nộp cả bộ.";
            }
            if (m.Purpose is "Practice" or "Review")
            {
                var pool = m.DifficultyFloor.HasValue ? subjectQuestions.Count(q => q.PrimaryTopicNodeId.ToString() == m.SkillId && q.Difficulty >= m.DifficultyFloor)
                    : LearningPathInsights.PracticePool(subjectQuestions.Where(q => q.PrimaryTopicNodeId.ToString() == m.SkillId).ToList(), usedIds.ToHashSet()).Count;
                poolSizes[m.MissionId] = pool;
                // Sets keep a fixed size; routes built before the loop may hold larger sets, which are capped here.
                var skillSetSize = plan.PracticeQuestionsBySkill.GetValueOrDefault(m.SkillId, plan.PracticeQuestionsPerSet);
                item.QuestionCount = (uint)Math.Min(Math.Min(m.QuestionCount == 0 ? skillSetSize : m.QuestionCount, skillSetSize), pool);
                item.DefaultQuestionCount = item.QuestionCount;
                item.CanStart = item.QuestionCount > 0 && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped && (!m.AvailableFrom.HasValue || m.AvailableFrom <= Now);
                item.EstimatedDurationMinutes = Math.Max(1u, item.QuestionCount) * LearningPathPacing.MinutesPerQuestion;
            }
            ApplyRetryRule(item, m, session, latest, active, runs, path, fresh, poolSizes.GetValueOrDefault(m.MissionId));
            if (plan.IsPlacement)
            {
                item.WeaknessLevel = null;
                item.CanRetry = false;
                item.RetryLabel = null;
                item.RetryBlockedReason = null;
                if (latest?.Status == SessionRunStatus.Completed) item.Status = "Completed";
            }
            result.Missions.Add(item);
            if (m.Purpose == "Review" && !m.IsSelfAssessedCheck && session.Status != LearningPathSessionStatus.Completed && session.Status != LearningPathSessionStatus.Skipped) result.ReviewDueTasks.Add(item);
            if (active != null) { result.OngoingRuns.Add(item); continue; }
            if (item.Status == "Completed" || session.Status == LearningPathSessionStatus.Completed || !item.CanStart) continue;
            // New confirmed evidence (including assignments) skips redundant future guided work.
            if (skill.State == "Confirmed" && m.Purpose != "Review" && !m.DifficultyFloor.HasValue && !m.IsRepair) continue;
            // The check is recommended after two passing sets in a row; it stays available on the route for confident students.
            if (m.Purpose == "IndependentCheck" && plan.PacingVersion >= 2 && !m.IsProbe && !milestone.ReadyForCheck &&
                milestone.Missions.Any(x => x.Purpose == "Practice" && x.CanStart))
            {
                item.Advice = $"Nên đạt {LearningPathPacing.ReadyStreak} bộ luyện liên tiếp từ 80% trước. Vẫn có thể làm ngay nếu em tự tin.";
                continue;
            }
            // Confirmation reviews of self-assessed parts come after the main work; they stay open on the route for students who want them earlier.
            if (m.IsSelfAssessedCheck) { finalChecks.Add(item); continue; }
            options.Add(item);
        }
        if (options.Count == 0) options.AddRange(finalChecks);
        else foreach (var check in finalChecks) check.Advice = "Bài ôn cuối lộ trình. Nên làm sau các phần chính; vẫn có thể làm sớm nếu em muốn.";
        // Interleaving: after several sets of one skill today, other skills come first.
        var localToday = LocalDate(Now);
        var todaySkills = runs.Where(r => r.Status == SessionRunStatus.Completed && r.CompletedAt.HasValue && LocalDate(r.CompletedAt.Value) == localToday)
            .OrderByDescending(r => r.CompletedAt).Select(r => path.Sessions.FirstOrDefault(x => x.SessionId == r.SessionId))
            .Where(x => x != null && x.Objective == "Practice").Select(x => x!.TopicNodeId.ToString()).ToList();
        var setsToday = todaySkills.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var lastSkill = todaySkills.FirstOrDefault();
        var streakToday = lastSkill == null ? 0 : todaySkills.TakeWhile(x => x == lastSkill).Count();
        options = options.OrderBy(m => m.Purpose == "Review" || m.Title.StartsWith("Rèn lại") ? 0 : 1)
            .ThenBy(m => m.SkillId == lastSkill && m.Purpose == "Practice" && streakToday >= LearningPathPacing.MaxSetsPerSkillPerDay && options.Any(o => o.SkillId != lastSkill) ? 1 : 0).ToList();
        BuildTodayPlan(result, options, weaknessBySkill, setsToday);
        result.PlanOutdated = plan.PacingVersion < 2;
        result.Progress = await ProgressAsync(centerId, studentId, subjectId, plan, map, result.Missions, cancellationToken);
        if (plan.IsPlacement)
            result.PlacementReport = await PlacementReportAsync(centerId, studentId, path, plan, map, runs, result.Missions, cancellationToken);
        var scopedSkills = plan.Milestones.Where(m => !m.IsSelfAssessedCheck).SelectMany(m => m.SkillIds).Distinct().ToHashSet();
        result.CanAdvance = !plan.IsPlacement && scopedSkills.Count > 0 && result.OngoingRuns.Count == 0 &&
            scopedSkills.All(id => map.Skills.FirstOrDefault(s => s.SkillId == id)?.State == "Confirmed") &&
            !result.Missions.Any(m => m.Status != "Completed" && m.Status != "Skipped" &&
                (result.GoalType == "Advanced" || m.Purpose == "Review" && m.AvailableFrom <= Now || m.Title.StartsWith("Rèn lại")));
        result.NextSkillIds = map.Skills.Where(s => !scopedSkills.Contains(s.SkillId) && !vouched.Contains(s.SkillId) && s.State != "Confirmed" &&
            s.Availability == "Available").Take(3).Select(s => s.SkillId).ToList();
        result.PrimaryMission = result.TodayPlan.FirstOrDefault();
        result.AlternativeOptions = result.TodayPlan.Where(m => m.MissionId != result.PrimaryMission?.MissionId && m.ActiveRunId == null).Take(2).ToList();
        if (result.PrimaryMission == null) result.AgendaMessage = options.Count > 0
            ? "Chưa có nhiệm vụ đủ ngắn cho thời gian này. Hãy kéo thêm thời gian hoặc chọn một hoạt động trong tuyến; bạn có thể lưu và tiếp tục."
            : "Các hoạt động khả dụng đã hoàn thành hoặc còn thiếu nội dung/bằng chứng. Xem từng chặng để biết phần cần bổ sung hoặc luyện thêm.";
        if (plan.IsPlacement) result.AgendaMessage = result.PlacementReport!.IsComplete
            ? "Đã hoàn thành kiểm tra đầu vào. Xem đánh giá từng kỹ năng và lập tuyến học từ kết quả."
            : "Hoàn thành các phần kiểm tra đầu vào dưới đây. Bài làm được lưu; kết quả cập nhật sau khi chấm xong từng phần.";
        return result;
    }

    /// <summary>
    /// Fills today's budget in priority order (in-progress work, due reviews, then the route). Practice sets keep a fixed size;
    /// spare time adds more sets for the weakest skills (at most three per skill a day). Never pads with filler.
    /// </summary>
    private static void BuildTodayPlan(LearningAgendaDto result, List<AgendaItemDto> options, Dictionary<string, decimal> weakness, Dictionary<string, int> setsToday)
    {
        var budget = result.AvailableMinutes;
        uint used = 0;
        var plan = new List<AgendaItemDto>();
        uint Minutes(AgendaItemDto i) => i.Purpose is "Practice" or "Review" ? i.QuestionCount * LearningPathPacing.MinutesPerQuestion * i.PlannedSets : i.EstimatedDurationMinutes;
        foreach (var ongoing in result.OngoingRuns) { plan.Add(ongoing); used += ongoing.EstimatedDurationMinutes; }
        foreach (var option in options)
        {
            var item = Clone(option); item.PlannedSets = 1;
            var cost = Minutes(item);
            // The first step always fits, even if a set runs a few minutes over: it can be paused.
            if (plan.Count > 0 && used + cost > budget) continue;
            plan.Add(item); used += cost;
            if (used >= budget) break;
        }
        while (true)
        {
            var grow = plan.Where(i => i.ActiveRunId == null && i.Purpose == "Practice" && i.QuestionCount > 0 &&
                    i.PlannedSets + setsToday.GetValueOrDefault(i.SkillId) < LearningPathPacing.MaxSetsPerSkillPerDay &&
                    (i.EstimatedSets == 0 || i.PlannedSets < Math.Max(1, (int)i.EstimatedSets - (int)Math.Max(0, (int)i.SetNumber - 1))) &&
                    used + i.QuestionCount * LearningPathPacing.MinutesPerQuestion <= budget)
                .OrderByDescending(i => weakness.GetValueOrDefault(i.SkillId)).ThenBy(i => i.PlannedSets).FirstOrDefault();
            if (grow == null) break;
            grow.PlannedSets++; used += grow.QuestionCount * LearningPathPacing.MinutesPerQuestion;
        }
        foreach (var i in plan.Where(i => i.Purpose is "Practice" or "Review" && i.ActiveRunId == null)) i.EstimatedDurationMinutes = Minutes(i);
        result.TodayPlan = plan;
        result.PlannedMinutes = used;
        result.PlannedQuestionCount = (uint)plan.Sum(i => i.Purpose is "Study" ? 0 : (int)(i.QuestionCount * Math.Max(1, i.PlannedSets)));
        var notes = new List<string>();
        if (plan.Count > 0 && used + 10 < budget)
            notes.Add($"Hôm nay có khoảng {LearningPathPacing.FormatMinutes(used)} việc phù hợp trong {LearningPathPacing.FormatMinutes(budget)} bạn chọn. Hệ thống không lặp câu để lấp thời gian; bộ tiếp theo được tạo sau khi làm xong bộ trước.");
        if (used > 50) notes.Add("Nên nghỉ 5–10 phút sau mỗi khoảng 45 phút học. Có thể tạm dừng bất cứ lúc nào.");
        result.PacingNote = notes.Count == 0 ? null : string.Join(" ", notes);
    }

    private static DateOnly LocalDate(DateTime utc)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (Exception) { zone = TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7"); }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
    }

    /// <summary>Overall progress toward the goal: confirmed skills, an honest finish estimate and the study streak.</summary>
    private async Task<GoalProgressDto> ProgressAsync(Guid centerId, Guid studentId, Guid subjectId, PathProposalDto plan, CapabilityMapDto map,
        List<AgendaItemDto> missions, CancellationToken ct)
    {
        // Self-assessed parts awaiting their confirmation review are not part of the learning goal's progress.
        var skills = plan.Milestones.Where(m => !m.IsSelfAssessedCheck).SelectMany(m => m.SkillIds).Distinct().ToList();
        var confirmed = skills.Count(id => map.Skills.FirstOrDefault(s => s.SkillId == id)?.State == "Confirmed");
        var ready = plan.Milestones.Count(m => m.ReadyForCheck && m.SkillIds.All(id => map.Skills.FirstOrDefault(s => s.SkillId == id)?.State != "Confirmed"));
        var open = missions.Where(m => m.Status is not ("Completed" or "Skipped") && !m.AvailableFrom.HasValue).Sum(m => (int)m.EstimatedDurationMinutes);
        // Sets the loop is still expected to create, per unconfirmed skill.
        var expected = plan.Milestones.Where(m => m.SkillIds.All(id => map.Skills.FirstOrDefault(s => s.SkillId == id)?.State != "Confirmed") && !m.ReadyForCheck)
            .Sum(m => Math.Max(0, (int)(m.Missions.Where(x => x.Purpose == "Practice").Select(x => x.EstimatedSets).DefaultIfEmpty(0u).Max())
                - m.Missions.Count(x => x.Purpose == "Practice")) *
                (int)(plan.PracticeQuestionsBySkill.GetValueOrDefault(m.SkillIds.First(), plan.PracticeQuestionsPerSet) * LearningPathPacing.MinutesPerQuestion));
        var remaining = (uint)Math.Max(0, open + expected);
        var goal = await db.LearningGoals.AsNoTracking().Where(g => g.CenterId == centerId && g.StudentId == studentId && g.SubjectId == subjectId && g.Status == "Active")
            .OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync(ct);
        var perDay = goal == null ? 0d : goal.AvailableMinutesPerDay * goal.AvailableDaysPerWeek / 7d;
        var since = Now.AddDays(-60);
        var days = (await db.Attempts.AsNoTracking().Where(a => a.CenterId == centerId && a.StudentId == studentId && a.Question.SubjectId == subjectId && a.CreatedAt >= since)
            .Select(a => a.CreatedAt).ToListAsync(ct)).Select(LocalDate).ToHashSet();
        var today = LocalDate(Now);
        var cursor = days.Contains(today) ? today : today.AddDays(-1);
        uint streak = 0;
        while (days.Contains(cursor)) { streak++; cursor = cursor.AddDays(-1); }
        return new GoalProgressDto
        {
            TotalSkills = (uint)skills.Count, ConfirmedSkills = (uint)confirmed, ReadyForCheckSkills = (uint)ready, RemainingMinutes = remaining,
            EstimatedFinishDate = perDay > 0 && remaining > 0 ? Now.Date.AddDays(Math.Ceiling(remaining / perDay)) : null,
            StreakDays = streak, StudiedToday = days.Contains(today)
        };
    }

    private static AgendaItemDto Clone(AgendaItemDto i) => new()
    {
        AvailableFrom = i.AvailableFrom, QuestionCount = i.QuestionCount, DefaultQuestionCount = i.DefaultQuestionCount, MissionId = i.MissionId,
        LegacySessionId = i.LegacySessionId, Title = i.Title, Objective = i.Objective, Purpose = i.Purpose, SkillId = i.SkillId, SkillName = i.SkillName,
        EstimatedDurationMinutes = i.EstimatedDurationMinutes, ActivityTypes = i.ActivityTypes, RecommendationReason = i.RecommendationReason,
        CanStart = i.CanStart, Status = i.Status, ActiveRunId = i.ActiveRunId, LatestRunId = i.LatestRunId, WeaknessLevel = i.WeaknessLevel,
        CanRetry = i.CanRetry, RetryLabel = i.RetryLabel, RetryBlockedReason = i.RetryBlockedReason, Runs = i.Runs,
        PlannedSets = i.PlannedSets, SetNumber = i.SetNumber, EstimatedSets = i.EstimatedSets, Advice = i.Advice
    };

    /// <summary>
    /// Learning activities can always be repeated (a new run, history kept). A failed independent check can be retaken only
    /// after a practice set on the same skill finished later than that check, and only with questions the student has not seen.
    /// </summary>
    private static void ApplyRetryRule(AgendaItemDto item, ProposedMissionDto m, LearningPathSession session, LearningPathSessionRun? latest,
        LearningPathSessionRun? active, List<LearningPathSessionRun> runs, LearningPath path, int fresh, int pool)
    {
        if (active != null || latest == null || latest.Status != SessionRunStatus.Completed) return;
        if (m.Purpose == "IndependentCheck")
        {
            item.RetryLabel = "Kiểm tra lại";
            if (latest.Outcome == SessionRunOutcome.Passed || session.Status == LearningPathSessionStatus.Completed)
            { item.RetryBlockedReason = "Đã đạt kiểm tra độc lập. Lịch ôn sẽ kiểm tra lại khả năng nhớ, không cần làm lại ngay."; return; }
            var practiceSessions = path.Sessions.Where(s => s.TopicNodeId == session.TopicNodeId && s.Objective is "Practice" or "Review")
                .Select(s => s.SessionId).ToHashSet();
            var repaired = runs.Any(r => practiceSessions.Contains(r.SessionId) && r.Status == SessionRunStatus.Completed && r.CompletedAt > latest.CompletedAt);
            if (!repaired) { item.RetryBlockedReason = "Hoàn thành một bộ rèn lại của kỹ năng này trước, rồi mới kiểm tra lại bằng câu mới."; return; }
            if (fresh < 5) { item.RetryBlockedReason = $"Còn {fresh}/5 câu chưa gặp; cần giáo viên bổ sung câu để kiểm tra lại công bằng."; return; }
            item.CanRetry = true;
            return;
        }
        if (m.Purpose is "Practice" or "Review")
        {
            item.RetryLabel = "Luyện thêm";
            if (pool == 0) { item.RetryBlockedReason = "Chưa có câu phù hợp để luyện thêm."; return; }
            item.CanRetry = true;
            return;
        }
        item.RetryLabel = "Học lại";
        item.CanRetry = true;
    }

    private async Task<List<PathHistoryItemDto>> PathHistoryAsync(Guid centerId, Guid studentId, Guid subjectId, Guid? activePathId, CancellationToken ct)
    {
        var older = await db.LearningPaths.AsNoTracking().Include(p => p.Sessions).Where(p => p.CenterId == centerId && p.StudentId == studentId &&
            p.SubjectId == subjectId && p.PlanSchemaVersion == "4.0" && p.LearningPathId != activePathId)
            .OrderByDescending(p => p.Version).Take(5).ToListAsync(ct);
        if (older.Count == 0) return [];

        var counts = await (
            from p in db.LearningPaths.AsNoTracking()
            where p.CenterId == centerId && p.StudentId == studentId && p.SubjectId == subjectId &&
                  p.PlanSchemaVersion == "4.0" && p.LearningPathId != activePathId
            join s in db.LearningPathSessions.AsNoTracking() on p.LearningPathId equals s.LearningPathId
            join r in db.LearningPathSessionRuns.AsNoTracking() on s.SessionId equals r.SessionId
            join a in db.ActivityRuns.AsNoTracking() on r.SessionRunId equals a.SessionRunId
            join i in db.IssuedItems.AsNoTracking() on a.ActivityRunId equals i.ActivityRunId
            where i.AttemptId != null
            group i by p.LearningPathId into g
            select new { LearningPathId = g.Key, Count = g.Count() }
        ).ToDictionaryAsync(x => x.LearningPathId, x => (uint)x.Count, ct);

        return older.Select(p => new PathHistoryItemDto
        {
            LearningPathId = p.LearningPathId, Version = p.Version, Status = p.Status.ToString(), CreatedAt = p.CreatedAt,
            EndedAt = p.Status == LearningPathStatus.Active ? null : p.UpdatedAt, GoalTitle = GoalTitleOf(p.PlanJson) ?? $"Tuyến phiên bản {p.Version}",
            TotalMissions = (uint)p.Sessions.Count, CompletedMissions = (uint)p.Sessions.Count(s => s.Status == LearningPathSessionStatus.Completed),
            AnsweredQuestions = counts.GetValueOrDefault(p.LearningPathId, 0u)
        }).ToList();
    }

    private static string? GoalTitleOf(JsonDocument? plan)
    {
        if (plan == null || plan.RootElement.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in plan.RootElement.EnumerateObject())
            if (string.Equals(property.Name, "goalTitle", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        return null;
    }

    private static LearningGoalDto GoalDto(LearningGoal g) => new()
    {
        GoalId = g.GoalId, StudentId = g.StudentId, SubjectId = g.SubjectId, ClassId = g.ClassId, Title = g.Title, GoalType = g.GoalType,
        Description = g.Description, TargetSkillIds = LearningGoalScope.Read(g.TargetSkillIds).TargetSkillIds,
        SelfReportedPrerequisiteSkillIds = LearningGoalScope.Read(g.TargetSkillIds).SelfReportedPrerequisiteSkillIds,
        SelfAssessedSkillIds = LearningGoalScope.Read(g.TargetSkillIds).SelfAssessedSkillIds,
        DeadlineDate = g.DeadlineDate, AvailableMinutesPerDay = g.AvailableMinutesPerDay, AvailableDaysPerWeek = g.AvailableDaysPerWeek,
        PreferredMode = g.PreferredMode, PreferredPace = g.PreferredPace, Status = g.Status, CreatedAt = g.CreatedAt, UpdatedAt = g.UpdatedAt
    };
}
