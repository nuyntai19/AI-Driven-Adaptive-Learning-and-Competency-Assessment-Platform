using System.Text.Json;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed partial class LearningPathV4Planner
{
    /// <summary>
    /// Tests more parts inside the active route instead of replacing it: each part gets a 5-question check.
    /// Passing schedules spaced review; a low score starts repair sets (LearningPathFollowUp). Progress on the
    /// rest of the route is kept.
    /// </summary>
    public async Task<ExtendPathCheckResponse> ExtendPathWithCheckAsync(Guid centerId, Guid studentId, ExtendPathCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var wanted = request.SkillIds.Distinct().ToList();
        if (wanted.Count is 0 or > 50) throw new ArgumentException("Chọn ít nhất một phần để kiểm tra.");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await StudentLockHelper.AcquireStudentLockAsync(db, centerId, studentId, cancellationToken);
        var path = await db.LearningPaths.Include(p => p.Sessions).Where(p => p.CenterId == centerId && p.StudentId == studentId &&
            p.SubjectId == request.SubjectId && p.PlanSchemaVersion == "4.0" && p.Status == LearningPathStatus.Active)
            .OrderByDescending(p => p.Version).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Chưa có lộ trình đang học. Hãy làm kiểm tra đầu vào để lập lộ trình.");
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson!.RootElement.GetRawText(), LearningPathV4Data.Json)
            ?? throw new InvalidOperationException("Không đọc được lộ trình hiện tại.");
        if (plan.IsPlacement) throw new InvalidOperationException("Đang trong bài kiểm tra đầu vào; hãy làm tiếp các phần còn lại.");
        var classId = path.Sessions.Select(s => s.ClassId).FirstOrDefault() ?? request.ClassId;
        var map = await LearningPathV4Data.MapAsync(db, centerId, studentId, request.SubjectId, classId, Now, cancellationToken);
        var nodes = await LearningPathV4Data.NodesAsync(db, centerId, studentId, request.SubjectId, classId, cancellationToken);
        if (wanted.Any(id => map.Skills.All(s => s.SkillId != id)))
            throw new ArgumentException("Phần đã chọn không thuộc môn và giáo trình đang học.");
        var questions = await LearningPathV4Data.QuestionsAsync(db, centerId, request.SubjectId, nodes.Select(n => n.NodeId).ToList(), cancellationToken);
        var used = await db.Attempts.AsNoTracking().Where(a => a.CenterId == centerId && a.StudentId == studentId &&
            a.Question.SubjectId == request.SubjectId).Select(a => a.QuestionId).Distinct().ToListAsync(cancellationToken);
        var exposed = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == centerId && i.ActivityRun.SessionRun.StudentId == studentId &&
            i.SolutionExposedAt != null && i.QuestionId != null).Select(i => i.QuestionId!.Value).Distinct().ToListAsync(cancellationToken);
        var response = new ExtendPathCheckResponse();
        var order = path.Sessions.Count == 0 ? 0u : path.Sessions.Max(s => s.SessionOrder);
        foreach (var id in wanted)
        {
            var node = nodes.Single(n => n.NodeId.ToString() == id);
            var fresh = questions.Count(q => q.PrimaryTopicNodeId == node.NodeId && !used.Contains(q.QuestionId) && !exposed.Contains(q.QuestionId));
            if (fresh < LearningPathPacing.IndependentCheckQuestions) { response.UnavailableSkillIds.Add(id); continue; }
            var milestone = plan.Milestones.FirstOrDefault(m => m.SkillIds.Contains(id));
            if (milestone == null)
            {
                milestone = new ProposedMilestoneDto { Order = (uint)plan.Milestones.Count + 1, Title = node.NodeName, SkillIds = [id],
                    OutcomeDescription = node.Description ?? $"Tự thực hiện được các nhiệm vụ về {node.NodeName}." };
                plan.Milestones.Add(milestone);
            }
            // A full check replaces the short confirmation of a self-assessed part.
            if (milestone.IsSelfAssessedCheck)
            {
                milestone.IsSelfAssessedCheck = false;
                foreach (var review in milestone.Missions.Where(m => m.IsSelfAssessedCheck))
                {
                    var session = path.Sessions.FirstOrDefault(s => s.SessionId.ToString() == review.MissionId);
                    if (session is { Status: LearningPathSessionStatus.NotStarted }) { session.Status = LearningPathSessionStatus.Skipped; session.UpdatedAt = Now; session.RowVersion++; }
                }
            }
            plan.SelfAssessedSkillIds.Remove(id); plan.SelfReportedPrerequisiteSkillIds.Remove(id);
            if (!plan.RequestedSkillIds.Contains(id)) plan.RequestedSkillIds.Add(id);
            var check = new ProposedMissionDto
            {
                MissionId = Guid.NewGuid().ToString(), SkillId = id, Purpose = "IndependentCheck", ActivityTypes = ["IndependentCheck"],
                Title = $"Kiểm tra thêm: {node.NodeName}", QuestionCount = LearningPathPacing.IndependentCheckQuestions, EstimatedMinutes = 10,
                CanStart = true, IsProbe = true,
                SelectionReason = "Em chọn kiểm tra thêm phần này. 5 câu mới; đạt thì giữ lịch ôn, chưa đạt sẽ thêm bộ rèn lại ngay trong lộ trình hiện tại."
            };
            milestone.Missions.Insert(0, check);
            // Add through the set: a new session with a preset key attached via the navigation would be treated as an update.
            db.LearningPathSessions.Add(new LearningPathSession
            {
                SessionId = Guid.Parse(check.MissionId), CenterId = centerId, LearningPathId = path.LearningPathId, TopicNodeId = node.NodeId,
                ClassId = classId, Title = check.Title, Objective = check.Purpose, Rationale = check.SelectionReason,
                PolicyVersion = LearningPathV4Data.Policy, MinimumQuestions = LearningPathPacing.IndependentCheckQuestions, RequiredCorrectRate = .8m,
                PhaseNumber = milestone.Order, WeekNumber = milestone.Order, SessionOrder = ++order, CreatedAt = Now, UpdatedAt = Now, RowVersion = 1
            });
            response.MissionIds.Add(check.MissionId);
        }
        if (response.MissionIds.Count > 0)
        {
            path.PlanJson = JsonSerializer.SerializeToDocument(plan, LearningPathV4Data.Json); path.UpdatedAt = Now; path.RowVersion++;
            var goal = await db.LearningGoals.FirstOrDefaultAsync(g => g.CenterId == centerId && g.StudentId == studentId && g.GoalId == plan.GoalId, cancellationToken);
            if (goal != null)
            {
                var scope = LearningGoalScope.Read(goal.TargetSkillIds);
                scope.TargetSkillIds = scope.TargetSkillIds.Union(wanted.Except(response.UnavailableSkillIds)).ToList();
                scope.SelfAssessedSkillIds = scope.SelfAssessedSkillIds.Except(scope.TargetSkillIds).ToList();
                scope.SelfReportedPrerequisiteSkillIds = scope.SelfReportedPrerequisiteSkillIds.Except(scope.TargetSkillIds).ToList();
                goal.TargetSkillIds = scope.Store(); goal.UpdatedAt = Now; goal.RowVersion++;
            }
            db.PathDecisions.Add(new PathDecision { CenterId = centerId, LearningPathId = path.LearningPathId,
                TriggerType = $"ExtendCheck:{Guid.NewGuid():N}", PolicyVersion = LearningPathV4Data.Policy,
                DecisionSummary = $"Thêm kiểm tra {response.MissionIds.Count} phần vào lộ trình hiện tại; tiến độ các phần khác được giữ.",
                ReasonCodes = JsonSerializer.SerializeToDocument(new[] { "EXTEND_CHECK" }), CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
            await db.SaveChangesAsync(cancellationToken);
        }
        await tx.CommitAsync(cancellationToken);
        return response;
    }
}
