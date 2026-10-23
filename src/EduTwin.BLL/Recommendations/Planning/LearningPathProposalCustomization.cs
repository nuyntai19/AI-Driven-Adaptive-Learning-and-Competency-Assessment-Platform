using System.Text.Json;
using System.Text.RegularExpressions;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed partial class LearningPathV4Planner
{
    public async Task<PathProposalDto> CustomizePathProposalAsync(Guid centerId, Guid studentId, Guid proposalId,
        CustomizePathProposalRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AvailableMinutesPerDay is < 5 or > 300 || request.AvailableDaysPerWeek is < 1 or > 7 ||
            request.PracticeQuestionsPerSet is < 3 or > 20 || request.TargetSkillIds is not { Count: > 0 and <= 200 } ||
            request.PracticeQuestionsBySkill is null || request.PracticeQuestionsBySkill.Count > 200 ||
            request.PracticeQuestionsBySkill.Values.Any(count => count is < 3 or > 20))
            throw new ArgumentException("Chọn ít nhất một phần học, 3–20 câu mỗi bộ và lịch học hợp lệ.");
        var source = await db.PathProposals.AsNoTracking().Include(p => p.Goal).FirstOrDefaultAsync(p =>
            p.CenterId == centerId && p.StudentId == studentId && p.ProposalId == proposalId, cancellationToken)
            ?? throw new KeyNotFoundException("Đề xuất không thuộc về bạn.");
        if (source.Status != "Pending" || source.ExpiresAt <= Now)
            throw new InvalidOperationException("Đề xuất đã hết hạn hoặc đã được chọn. Hãy mở đề xuất mới.");
        var plan = JsonSerializer.Deserialize<PathProposalDto>(source.CandidatePlan.RootElement.GetRawText(), LearningPathV4Data.Json)
            ?? throw new InvalidOperationException("Không đọc được tuyến đề xuất.");
        var originalSkills = plan.Milestones.SelectMany(m => m.SkillIds).ToHashSet();
        // Confirmation reviews of self-assessed parts always stay at the end of the route; they are not learning targets.
        var selfAssessed = plan.SelfAssessedSkillIds.ToHashSet();
        var selected = request.TargetSkillIds.Where(id => !selfAssessed.Contains(id)).ToHashSet();
        if (selected.Count == 0) throw new ArgumentException("Chọn ít nhất một phần học ngoài các bài ôn xác nhận.");
        if (!selected.IsSubsetOf(originalSkills)) throw new ArgumentException("Chỉ tùy chỉnh các phần trong tuyến đang xem.");
        if (!request.PracticeQuestionsBySkill.Keys.ToHashSet().IsSubsetOf(originalSkills))
            throw new ArgumentException("Chỉ phân bổ câu hỏi cho các phần trong tuyến đang xem.");
        var map = await GetCapabilityMapAsync(centerId, studentId, source.SubjectId, source.Goal.ClassId, cancellationToken);
        var prerequisites = map.Skills.ToDictionary(s => s.SkillId, s => s.PrerequisiteSkillIds);
        var pending = new Queue<string>(selected);
        while (pending.TryDequeue(out var skill))
        {
            if (!prerequisites.TryGetValue(skill, out var required))
                throw new InvalidOperationException("Phạm vi giáo trình đã thay đổi. Hãy tạo đề xuất mới.");
            foreach (var prerequisite in required.Where(id => originalSkills.Contains(id) && !plan.SelfReportedPrerequisiteSkillIds.Contains(id) && !selfAssessed.Contains(id)))
                if (selected.Add(prerequisite)) pending.Enqueue(prerequisite);
        }
        var nodes = await LearningPathV4Data.NodesAsync(db, centerId, studentId, source.SubjectId, source.Goal.ClassId, cancellationToken);
        var questions = await LearningPathV4Data.QuestionsAsync(db, centerId, studentId, source.SubjectId, nodes.Select(n => n.NodeId).ToList(), source.Goal.ClassId, cancellationToken);
        var used = (await db.Attempts.AsNoTracking().Where(a => a.CenterId == centerId && a.StudentId == studentId &&
            a.Question.SubjectId == source.SubjectId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken)).ToHashSet();
        plan.Milestones = plan.Milestones.Where(m => !m.IsSelfAssessedCheck && m.SkillIds.Any(selected.Contains))
            .Concat(plan.Milestones.Where(m => m.IsSelfAssessedCheck)).ToList();
        plan.RequestedSkillIds = request.TargetSkillIds.Where(id => !selfAssessed.Contains(id)).Distinct().ToList();
        uint order = 0;
        foreach (var milestone in plan.Milestones)
        {
            milestone.Order = ++order;
            foreach (var mission in milestone.Missions)
            {
                // Every preview can be accepted separately: never reuse session primary keys.
                mission.MissionId = Guid.NewGuid().ToString();
                if (mission.Purpose != "Practice" || mission.IsProbe) continue;
                var desiredCount = request.PracticeQuestionsBySkill.GetValueOrDefault(mission.SkillId, request.PracticeQuestionsPerSet);
                var pool = questions.Where(q => q.PrimaryTopicNodeId.ToString() == mission.SkillId).ToList();
                pool = mission.DifficultyFloor.HasValue ? pool.Where(q => q.Difficulty >= mission.DifficultyFloor).ToList()
                    : LearningPathInsights.PracticePool(pool, used);
                mission.QuestionCount = Math.Min(desiredCount, (uint)pool.Count);
                mission.EstimatedMinutes = Math.Max(1u, mission.QuestionCount) * LearningPathPacing.MinutesPerQuestion;
                mission.CanStart = mission.CanStart && mission.QuestionCount > 0;
                mission.SelectionReason = Regex.Replace(mission.SelectionReason, @"mỗi bộ \d+ câu", $"mỗi bộ {desiredCount} câu")
                    + $" Bạn chọn tối đa {desiredCount} câu/bộ cho phần này; hiện có {mission.QuestionCount} câu phù hợp.";
            }
        }
        var goal = new LearningGoal
        {
            GoalId = Guid.NewGuid(), CenterId = centerId, StudentId = studentId, SubjectId = source.SubjectId,
            ClassId = source.Goal.ClassId, Title = source.Goal.Title, GoalType = source.Goal.GoalType,
            Description = source.Goal.Description, PreferredMode = source.Goal.PreferredMode, PreferredPace = source.Goal.PreferredPace,
            TargetSkillIds = new LearningGoalScope { TargetSkillIds = plan.RequestedSkillIds,
                SelfReportedPrerequisiteSkillIds = plan.SelfReportedPrerequisiteSkillIds, SelfAssessedSkillIds = plan.SelfAssessedSkillIds }.Store(),
            AvailableMinutesPerDay = request.AvailableMinutesPerDay, AvailableDaysPerWeek = request.AvailableDaysPerWeek,
            DeadlineDate = request.DeadlineDate, Status = "Draft", CreatedAt = Now, UpdatedAt = Now, RowVersion = 1
        };
        plan.ProposalId = Guid.NewGuid(); plan.GoalId = goal.GoalId;
        plan.PracticeQuestionsPerSet = request.PracticeQuestionsPerSet;
        plan.PracticeQuestionsBySkill = plan.Milestones.SelectMany(m => m.SkillIds).Distinct()
            .ToDictionary(id => id, id => request.PracticeQuestionsBySkill.GetValueOrDefault(id, request.PracticeQuestionsPerSet));
        plan.AvailableMinutesPerDay = goal.AvailableMinutesPerDay; plan.AvailableDaysPerWeek = goal.AvailableDaysPerWeek;
        plan.DeadlineDate = goal.DeadlineDate;
        plan.ConfirmedSkills = plan.ConfirmedSkills.Where(selected.Contains).ToList();
        plan.DevelopingSkills = plan.DevelopingSkills.Where(selected.Contains).ToList();
        plan.UnknownSkills = plan.UnknownSkills.Where(selected.Contains).ToList();
        var candidates = plan.Milestones.SelectMany(m => m.Missions).Where(m => m.CanStart).ToList();
        plan.InitialMission = candidates.FirstOrDefault(); plan.AlternativeOption = candidates.Skip(1).FirstOrDefault();
        var insights = await LearningPathInsights.ReadAsync(db, centerId, studentId, source.SubjectId, goal.ClassId, Now, cancellationToken);
        plan.StartingBaselineSummary = $"{insights.AnswerCount} lượt trả lời trong 30 ngày. {insights.Summary} Tuyến đã tùy chỉnh: {selected.Count} phần học, gồm nền tảng cần thiết; giữ định hướng {goal.Title}.";
        var totalMinutes = plan.Milestones.Sum(m => m.Missions.Sum(a => (int)(a.EstimatedMinutes +
            (a.Purpose == "Practice" && a.EstimatedSets > 1 ? (a.EstimatedSets - 1) *
                plan.PracticeQuestionsBySkill.GetValueOrDefault(a.SkillId, plan.PracticeQuestionsPerSet) * LearningPathPacing.MinutesPerQuestion : 0u))));
        plan.EstimatedWeeks = (uint)Math.Max(1, Math.Ceiling(totalMinutes / (decimal)(goal.AvailableMinutesPerDay * goal.AvailableDaysPerWeek)));
        var budget = goal.DeadlineDate.HasValue ? Math.Max(0, (goal.DeadlineDate.Value - Now).TotalDays / 7) * goal.AvailableMinutesPerDay * goal.AvailableDaysPerWeek : double.MaxValue;
        plan.FeasibilityStatus = budget < totalMinutes ? "InsufficientTime" : "Feasible";
        plan.FeasibilityMessage = $"Ước lượng khoảng {Math.Max(1, plan.EstimatedWeeks - 1)}–{plan.EstimatedWeeks + 1} tuần với tổng quỹ {goal.AvailableMinutesPerDay} phút/ngày, {goal.AvailableDaysPerWeek} ngày/tuần; số câu mỗi bộ theo từng phần học. Tự kiểm tra vẫn gồm 5 câu độc lập."
            + (plan.FeasibilityStatus == "InsufficientTime" ? " Lịch hiện tại chưa đủ trước mốc đã chọn; bạn có thể điều chỉnh thời gian hoặc phần học." : "");
        plan.ExpiresAt = Now.AddDays(1); plan.Status = "Pending";
        db.LearningGoals.Add(goal);
        db.PathProposals.Add(new PathProposal
        {
            ProposalId = plan.ProposalId, CenterId = centerId, StudentId = studentId, GoalId = goal.GoalId, SubjectId = goal.SubjectId,
            StartingBaselineSummary = plan.StartingBaselineSummary, CandidatePlan = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json),
            EstimatedWeeks = plan.EstimatedWeeks, FeasibilityStatus = plan.FeasibilityStatus, FeasibilityMessage = plan.FeasibilityMessage,
            ExpiresAt = plan.ExpiresAt, Status = "Pending", CreatedAt = Now, UpdatedAt = Now, RowVersion = 1
        });
        // A preview only creates drafts; the accepted goal, path and work remain active until acceptance.
        await db.SaveChangesAsync(cancellationToken);
        return plan;
    }
}
