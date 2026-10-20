using System.Text.Json;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>Subject/curriculum-scoped source data shared by planning and execution. No pilot fixtures.</summary>
public static class LearningPathV4Data
{
    public const string Policy = "v4-evidence-1";
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<List<KnowledgeNode>> NodesAsync(EduTwinDbContext db, Guid center, Guid student,
        Guid subject, Guid? classId, CancellationToken ct)
    {
        if (!await db.Students.AnyAsync(s => s.CenterId == center && s.StudentId == student, ct))
            throw new KeyNotFoundException("Không tìm thấy hồ sơ học sinh trong phạm vi được phép.");
        if (!await db.Subjects.AnyAsync(s => s.CenterId == center && s.SubjectId == subject, ct))
            throw new KeyNotFoundException("Môn học không tồn tại.");
        var scope = await StudentLearningScope.ResolveAsync(db, center, student, subject, classId, false, ct);
        if (!scope.Allowed) throw new LearningScopeDeniedException(scope.Reason!);
        var all = await db.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == center &&
            n.SubjectId == subject && n.IsActive && n.NodeType != NodeType.Subject && n.NodeType != NodeType.Chapter)
            .OrderBy(n => n.OrderIndex).ThenBy(n => n.NodeId).ToListAsync(ct);
        // Include leaf skills/concepts rather than repeatedly scheduling their parent (e.g. "Tenses").
        var parents = all.Where(n => n.ParentNodeId.HasValue).Select(n => n.ParentNodeId!.Value).ToHashSet();
        return all.Where(n => scope.Includes(n.NodeId) && !parents.Contains(n.NodeId)).ToList();
    }

    public static Task<List<Question>> QuestionsAsync(EduTwinDbContext db, Guid center, Guid subject,
        IReadOnlyCollection<ulong> nodes, CancellationToken ct) => db.Questions.AsNoTracking()
        .Where(q => q.CenterId == center && q.SubjectId == subject && nodes.Contains(q.PrimaryTopicNodeId) &&
            q.Status == QuestionStatus.Active && q.AnswerEvaluationMode != QuestionAnswerEvaluationMode.Manual &&
            q.QuestionType != QuestionType.Essay).OrderBy(q => q.Difficulty).ThenBy(q => q.QuestionId).ToListAsync(ct);

    public static bool Matches(LearningContentVersion content, KnowledgeNode node) =>
        content.SkillId == node.NodeId.ToString() || content.SkillId == node.NodeCode;

    public static async Task<CapabilityMapDto> MapAsync(EduTwinDbContext db, Guid center, Guid student,
        Guid subject, Guid? classId, DateTime now, CancellationToken ct)
    {
        var nodes = await NodesAsync(db, center, student, subject, classId, ct);
        var ids = nodes.Select(n => n.NodeId).ToList();
        var questions = await QuestionsAsync(db, center, subject, ids, ct);
        var content = await db.LearningContentVersions.AsNoTracking().Where(c => c.CenterId == center &&
            c.SubjectId == subject && c.Status == "Published").ToListAsync(ct);
        var attempts = await db.Attempts.AsNoTracking().Include(a => a.Question).Where(a => a.CenterId == center &&
            a.StudentId == student && a.Question.SubjectId == subject && ids.Contains(a.Question.PrimaryTopicNodeId) &&
            !a.Skipped).ToListAsync(ct);
        var attemptIds = attempts.Select(a => a.AttemptId).ToList();
        var evidence = await db.EvidenceAssessments.AsNoTracking().Where(e => e.CenterId == center &&
            attemptIds.Contains(e.AttemptId)).ToListAsync(ct);
        var latest = evidence.GroupBy(e => e.AttemptId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(e => e.EvaluatedAt).ThenByDescending(e => e.EvidenceAssessmentId).First());
        var analyses = await db.ReasoningAnalyses.AsNoTracking().Where(a => a.CenterId == center &&
            attemptIds.Contains(a.AttemptId)).ToListAsync(ct);
        var overrides = analyses.ToDictionary(a => a.AttemptId, a => a.OverrideIsCorrect);
        var twins = await db.KnowledgeTwins.AsNoTracking().Where(t => t.CenterId == center && t.StudentId == student &&
            t.SubjectId == subject && ids.Contains(t.TopicNodeId)).ToDictionaryAsync(t => t.TopicNodeId, ct);
        var edges = await db.KnowledgeEdges.AsNoTracking().Where(e => e.CenterId == center && e.SubjectId == subject &&
            e.RelationType == RelationType.PrerequisiteOf).ToListAsync(ct);
        var hierarchy = await db.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == center && n.SubjectId == subject && n.IsActive)
            .ToDictionaryAsync(n => n.NodeId, ct);
        bool Under(ulong leaf, ulong ancestor)
        {
            var visited = new HashSet<ulong>();
            while (visited.Add(leaf) && hierarchy.TryGetValue(leaf, out var current))
            {
                if (leaf == ancestor) return true;
                if (!current.ParentNodeId.HasValue) break;
                leaf = current.ParentNodeId.Value;
            }
            return false;
        }
        var statuses = new List<SkillCapabilityStatusDto>();
        foreach (var node in nodes)
        {
            var topic = node;
            var ancestors = new HashSet<ulong> { node.NodeId };
            while (topic.ParentNodeId.HasValue && hierarchy.TryGetValue(topic.ParentNodeId.Value, out var parent) &&
                parent.NodeType != NodeType.Subject && ancestors.Add(parent.NodeId))
            {
                topic = parent;
                if (topic.NodeType == NodeType.Topic) break;
            }
            var prerequisites = edges.Where(e => Under(node.NodeId, e.TargetNodeId)).SelectMany(e =>
                nodes.Where(n => Under(n.NodeId, e.SourceNodeId)).Select(n => n.NodeId.ToString())
                    .DefaultIfEmpty(e.SourceNodeId.ToString())).Where(id => id != node.NodeId.ToString()).Distinct().ToList();
            var related = attempts.Where(a => a.Question.PrimaryTopicNodeId == node.NodeId).ToList();
            var usable = related.Where(a => !a.IsPostFeedback && a.SolutionExposedAt == null &&
                a.CreatedAt >= now.AddDays(-30) && latest.TryGetValue(a.AttemptId, out var e) &&
                !e.RequiresTeacherReview && e.TrustLevel != EvidenceTrustLevel.ReviewOnly)
                .GroupBy(a => a.QuestionId).Select(g => g.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).First())
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).Take(5).ToList();
            bool Correct(DAL.AssessmentAndReasoning.Attempt a) => overrides.GetValueOrDefault(a.AttemptId) ?? a.IsCorrect ?? false;
            // Recent independent evidence can demonstrate recovery; old baseline errors must not permanently dilute a new check.
            var guessable = usable.Count(a => a.Question.QuestionType == QuestionType.MultipleChoice) * 2 > usable.Count;
            var confirmed = usable.Count >= 5 && usable.Count(Correct) >= (guessable ? usable.Count : Math.Ceiling(usable.Count * .8m));
            var state = confirmed ? "Confirmed" : usable.Count > 0
                ? (usable.Count(Correct) < usable.Count ? "Developing" : "Demonstrated") : "Unknown";
            var hasContent = content.Any(c => Matches(c, node)) || questions.Any(q => q.PrimaryTopicNodeId == node.NodeId) ||
                !string.IsNullOrWhiteSpace(node.Description);
            statuses.Add(new SkillCapabilityStatusDto
            {
                SkillId = node.NodeId.ToString(), SkillName = node.NodeName, TopicName = topic.NodeName, TopicId = topic.NodeId.ToString(),
                Description = node.Description ?? "", State = state, Availability = hasContent ? "Available" : "ContentUnavailable",
                EvidenceCount = (uint)usable.Count, RequiresProbe = usable.Count == 0,
                MasteryScore = twins.TryGetValue(node.NodeId, out var twin) && twin.EvidenceCount > 0 ? twin.MasteryPercentage : null,
                PrerequisiteSkillIds = prerequisites,
                Rationale = confirmed ? $"Đạt trên {usable.Count} câu độc lập có bằng chứng hợp lệ gần nhất trong 30 ngày."
                    : usable.Count > 0 ? $"Có {usable.Count} câu hợp lệ; cần tiếp tục học hoặc kiểm tra để xác nhận."
                    : related.Count > 0 ? "Có bài làm nhưng chưa đủ bằng chứng độc lập đã được xác nhận; không suy ra năng lực từ điểm tổng."
                    : "Chưa có bằng chứng cho kỹ năng này; không coi là năng lực thấp."
            });
        }
        foreach (var skill in statuses.Where(s => s.Availability == "Available"))
            if (skill.PrerequisiteSkillIds.Any(p => statuses.All(s => s.SkillId != p || s.State != "Confirmed")))
                skill.Availability = "PrerequisiteBlocked";
        return new CapabilityMapDto
        {
            SubjectId = subject, StudentId = student,
            SubjectName = await db.Subjects.Where(s => s.CenterId == center && s.SubjectId == subject).Select(s => s.SubjectName).SingleAsync(ct),
            Skills = statuses, LastUpdatedAt = now,
            OverallProgressRate = statuses.Count == 0 ? 0 : Math.Round(100m * statuses.Count(s => s.State == "Confirmed") / statuses.Count, 1),
            ProvenCapabilities = statuses.Where(s => s.State == "Confirmed").Select(s => s.SkillName).ToList(),
            AreasNeedingSupport = statuses.Where(s => s.State == "Developing").Select(s => s.SkillName).ToList()
        };
    }

    public static async Task<LearningPathSession> SessionAsync(EduTwinDbContext db, Guid center, Guid student, Guid id, CancellationToken ct)
    {
        var session = await db.LearningPathSessions.Include(s => s.LearningPath).FirstOrDefaultAsync(s =>
            s.CenterId == center && s.SessionId == id && s.LearningPath.StudentId == student, ct)
            ?? throw new KeyNotFoundException("Nhiệm vụ không tồn tại trong lộ trình của bạn.");
        var scope = await StudentLearningScope.ResolveAsync(db, center, student, session.LearningPath.SubjectId,
            session.ClassId, false, ct);
        if (!scope.Includes(session.TopicNodeId)) throw new LearningScopeDeniedException(scope.Reason ?? "Chủ đề nằm ngoài phạm vi học hiện tại.");
        if (session.PolicyVersion != Policy) throw new InvalidOperationException("Lộ trình cũ cần được tạo đề xuất mới trước khi học bằng nhiệm vụ.");
        return session;
    }
}
