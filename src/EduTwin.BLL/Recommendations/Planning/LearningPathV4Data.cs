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
    public const string Policy = "v4-evidence-grade-scope-2";
    public static bool IsV4Policy(string policy) => policy.StartsWith("v4-evidence-", StringComparison.Ordinal);
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<HashSet<ulong>> RecentQuestionIdsAsync(EduTwinDbContext db, Guid center, Guid student,
        DateTime now, uint delayDays, CancellationToken ct)
    {
        if (delayDays == 0) return [];
        var cutoff = now.AddDays(-(int)delayDays);
        var answered = await db.Attempts.AsNoTracking().Where(a => a.CenterId == center && a.StudentId == student && a.CreatedAt > cutoff)
            .Select(a => a.QuestionId).Distinct().ToListAsync(ct);
        var exposed = await db.IssuedItems.AsNoTracking().Where(i => i.CenterId == center && i.ActivityRun.SessionRun.StudentId == student &&
            i.QuestionId != null && i.SolutionExposedAt > cutoff).Select(i => i.QuestionId!.Value).Distinct().ToListAsync(ct);
        return answered.Concat(exposed).ToHashSet();
    }

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
            n.SubjectId == subject && n.IsActive && !n.IsDeleted && n.NodeType != NodeType.Subject && n.NodeType != NodeType.Chapter)
            .OrderBy(n => n.OrderIndex).ThenBy(n => n.NodeId).ToListAsync(ct);
        all = all.Where(n => scope.Includes(n.NodeId)).ToList();
        var topology = await ScopedKnowledgeTopology.ReadAsync(db, center, subject, scope, ct);
        foreach (var node in all)
            node.ParentNodeId = topology.Parents.GetValueOrDefault(node.NodeId);
        // Include leaf skills/concepts rather than repeatedly scheduling their parent (e.g. "Tenses").
        var included = all.Select(n => n.NodeId).ToHashSet();
        var parents = topology.Hierarchies.Where(h => scope.GraphVersionIds.Contains(h.Key))
            .SelectMany(h => h.Value.Where(n => included.Contains(n.Key) && n.Value.HasValue &&
                scope.NodeIdsByGraphVersionId.TryGetValue(h.Key, out var selected) && selected.Contains(n.Key) && selected.Contains(n.Value.Value))
                .Select(n => n.Value!.Value)).ToHashSet();
        return all.Where(n => !parents.Contains(n.NodeId)).ToList();
    }

    public static async Task<List<Question>> QuestionsAsync(EduTwinDbContext db, Guid center, Guid student, Guid subject,
        IReadOnlyCollection<ulong> nodes, Guid? classId, CancellationToken ct)
    {
        var scope = await StudentLearningScope.ResolveAsync(db, center, student, subject, classId, false, ct);
        if (!scope.Allowed) return [];
        var questions = await db.Questions.AsNoTracking()
        .Where(q => q.CenterId == center && q.SubjectId == subject && nodes.Contains(q.PrimaryTopicNodeId) &&
            q.Status == QuestionStatus.Active && !q.IsDeleted && q.AnswerEvaluationMode != QuestionAnswerEvaluationMode.Manual &&
            q.QuestionType != QuestionType.Essay).OrderBy(q => q.Difficulty).ThenBy(q => q.QuestionId).ToListAsync(ct);
        questions = questions.Where(q => scope.IncludesQuestion(q.PrimaryTopicNodeId, q.GradeLevel) && AutoGradable(q)).ToList();
        var choiceIds = questions.Where(q => q.QuestionType == QuestionType.MultipleChoice).Select(q => q.QuestionId).ToList();
        if (choiceIds.Count > 0)
        {
            var options = await db.QuestionOptions.AsNoTracking().Where(o => o.CenterId == center && choiceIds.Contains(o.QuestionId)).ToListAsync(ct);
            var validChoices = options.GroupBy(o => o.QuestionId).Where(g => g.Count() >= 2 && g.Count(o => o.IsCorrect) == 1)
                .Where(g => { var right = g.Single(o => o.IsCorrect); var answer = questions.Single(q => q.QuestionId == g.Key).CorrectAnswer?.Trim();
                    return string.Equals(answer, right.OptionLabel?.Trim(), StringComparison.OrdinalIgnoreCase) || answer == right.OptionId.ToString(); })
                .Select(g => g.Key).ToHashSet();
            questions = questions.Where(q => q.QuestionType != QuestionType.MultipleChoice || validChoices.Contains(q.QuestionId)).ToList();
        }
        return questions;
    }

    public static bool AutoGradable(Question question)
    {
        if (question.QuestionType == QuestionType.Essay || question.AnswerEvaluationMode == QuestionAnswerEvaluationMode.Manual ||
            string.IsNullOrWhiteSpace(question.CorrectAnswer) || question.MaxScore <= 0) return false;
        // Test the published reference against its actual grading mode, including numeric/coordinate/math configuration.
        var grader = new AssessmentAndReasoning.PreliminaryGrading.ShortAnswerGrader();
        return question.QuestionType == QuestionType.MultipleChoice && question.AnswerEvaluationMode == QuestionAnswerEvaluationMode.TextExact ||
            question.QuestionType == QuestionType.ShortAnswer && grader.Grade(question.CorrectAnswer, question.CorrectAnswer,
                new AssessmentAndReasoning.PreliminaryGrading.QuestionGradingContext { EvaluationMode = question.AnswerEvaluationMode,
                    MaxScore = question.MaxScore, Criteria = question.GradingCriteria }).IsCorrect == true;
    }

    public static bool Matches(LearningContentVersion content, KnowledgeNode node) =>
        content.SkillId == node.NodeId.ToString() || content.SkillId == node.NodeCode;

    public static async Task<CapabilityMapDto> MapAsync(EduTwinDbContext db, Guid center, Guid student,
        Guid subject, Guid? classId, DateTime now, CancellationToken ct)
    {
        var nodes = await NodesAsync(db, center, student, subject, classId, ct);
        var ids = nodes.Select(n => n.NodeId).ToList();
        var questions = await QuestionsAsync(db, center, student, subject, ids, classId, ct);
        var scope = await StudentLearningScope.ResolveAsync(db, center, student, subject, classId, false, ct);
        var topology = await ScopedKnowledgeTopology.ReadAsync(db, center, subject, scope, ct);
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
            t.SubjectId == subject && !t.IsDeleted).ToDictionaryAsync(t => t.TopicNodeId, ct);
        var edges = topology.Relations.Where(e => e.Edge.RelationType == RelationType.PrerequisiteOf).ToList();
        var hierarchy = await db.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == center && n.SubjectId == subject && n.IsActive && !n.IsDeleted)
            .ToDictionaryAsync(n => n.NodeId, ct);
        foreach (var node in hierarchy.Values) node.ParentNodeId = topology.Parents.GetValueOrDefault(node.NodeId);
        var externalMastery = edges.Select(e => e.Edge.SourceNodeId).Where(id => !ids.Contains(id) && hierarchy.ContainsKey(id))
            .Distinct().ToDictionary(id => id.ToString(), id => twins.TryGetValue(id, out var twin) && twin.EvidenceCount > 0 ? twin.MasteryPercentage : 0m);
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
            var prerequisites = edges.Where(e => topology.Under(node.NodeId, e.Edge.TargetNodeId, e.GraphVersionId)).SelectMany(e =>
                nodes.Where(n => topology.Under(n.NodeId, e.Edge.SourceNodeId, e.SourceGraphVersionId)).Select(n => n.NodeId.ToString())
                    .DefaultIfEmpty(e.Edge.SourceNodeId.ToString())).Where(id => id != node.NodeId.ToString()).Distinct().ToList();
            var related = attempts.Where(a => a.Question.PrimaryTopicNodeId == node.NodeId).ToList();
            // Learning-path attempts have no teacher review surface (the review queue is
            // assignment-scoped), so requiring teacher approval here would make Confirmed
            // unreachable from the path itself. The evaluator's auto-approve policy treats
            // path evidence as provisionally valid; mirror that: a session-run attempt is
            // usable when its latest evidence is not ReviewOnly, approval or not.
            // Assignment attempts keep the strict rule - a teacher can approve those.
            var usable = related.Where(a => !a.IsPostFeedback && a.SolutionExposedAt == null &&
                a.CreatedAt >= now.AddDays(-30) && latest.TryGetValue(a.AttemptId, out var e) &&
                (!e.RequiresTeacherReview || a.SessionRunId != null) && e.TrustLevel != EvidenceTrustLevel.ReviewOnly)
                .GroupBy(a => a.QuestionId).Select(g => g.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).First())
                .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId).Take(5).ToList();
            bool Correct(DAL.AssessmentAndReasoning.Attempt a) => overrides.GetValueOrDefault(a.AttemptId) ?? a.IsCorrect ?? false;
            // Recent independent evidence can demonstrate recovery; old baseline errors must not permanently dilute a new check.
            var guessable = usable.Count(a => a.Question.QuestionType == QuestionType.MultipleChoice) * 2 > usable.Count;
            // Floor aligned with LearningAssessmentPolicy.MinimumPlacementQuestions (3):
            // a hard floor of 5 made Confirmed impossible on any bank with fewer than 5
            // usable questions per skill, however well the student answered them.
            var confirmed = usable.Count >= LearningAssessmentPolicy.MinimumPlacementQuestions && usable.Count(Correct) >= (guessable ? usable.Count : Math.Ceiling(usable.Count * .8m));
            var state = confirmed ? "Confirmed" : usable.Count > 0
                ? (usable.Count(Correct) < usable.Count ? "Developing" : "Demonstrated") : "Unknown";
            var hasContent = content.Any(c => Matches(c, node)) || questions.Any(q => q.PrimaryTopicNodeId == node.NodeId) ||
                !string.IsNullOrWhiteSpace(node.Description);
            statuses.Add(new SkillCapabilityStatusDto
            {
                SkillId = node.NodeId.ToString(), SkillName = node.NodeName, TopicName = topic.NodeName, TopicId = topic.NodeId.ToString(),
                Description = node.Description ?? "", State = state, Availability = hasContent ? "Available" : "ContentUnavailable",
                EvidenceCount = (uint)usable.Count, RequiresProbe = usable.Count == 0,
                MasteryScore = twins.TryGetValue(node.NodeId, out var twin) && twin.EvidenceCount > 0 ? twin.MasteryPercentage : usable.Count > 0 ? Math.Round(100m * usable.Count(Correct) / usable.Count, 1) : null,
                PrerequisiteSkillIds = prerequisites,
                Rationale = confirmed ? $"Đạt trên {usable.Count} câu độc lập có bằng chứng hợp lệ gần nhất trong 30 ngày."
                    : usable.Count > 0 ? $"Có {usable.Count} câu hợp lệ; cần tiếp tục học hoặc kiểm tra để xác nhận."
                    : related.Count > 0 ? "Có bài làm nhưng chưa đủ bằng chứng độc lập đã được xác nhận; không suy ra năng lực từ điểm tổng."
                    : "Chưa có bằng chứng cho kỹ năng này; không coi là năng lực thấp."
            });
        }
        // A failed progress gate remains binding even if an 80% evidence window looks strong enough.
        // A mock is diagnostic and never participates in this gate.
        var activePaths = await db.LearningPaths.AsNoTracking().Where(p => p.CenterId == center && p.StudentId == student &&
            p.SubjectId == subject && p.PlanSchemaVersion == "4.0" && p.Status == LearningPathStatus.Active &&
            p.Sessions.Any(s => s.ClassId == classId)).Select(p => new { p.LearningPathId, p.PlanJson }).ToListAsync(ct);
        foreach (var path in activePaths)
        {
            var definition = path.PlanJson == null ? null : System.Text.Json.JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson.RootElement.GetRawText(), Json);
            var gates = definition?.Milestones.SelectMany(m => m.Missions).Where(m => m.IsMilestoneExam && !m.IsProbe).Select(m => m.MissionId).ToHashSet() ?? [];
            if (gates.Count == 0) continue;
            var finished = await db.LearningPathSessionRuns.AsNoTracking().Where(r => r.CenterId == center && r.StudentId == student &&
                r.Session.LearningPathId == path.LearningPathId && r.Status == SessionRunStatus.Completed)
                .Select(r => new { r.SessionId, r.Session.TopicNodeId, r.CompletedAt, r.SessionRunId, r.Outcome }).ToListAsync(ct);
            foreach (var gate in finished.Where(r => gates.Contains(r.SessionId.ToString())).GroupBy(r => r.TopicNodeId)
                .Select(g => g.OrderByDescending(r => r.CompletedAt).ThenByDescending(r => r.SessionRunId).First()))
            {
                if (gate.Outcome == SessionRunOutcome.Passed) continue;
                var skill = statuses.FirstOrDefault(s => s.SkillId == gate.TopicNodeId.ToString());
                if (skill == null) continue;
                skill.State = "Developing";
                skill.StageGateFailed = true;
                skill.Rationale = "Mốc kiểm tra chặng chưa đạt: hoàn thành phần rèn lại rồi kiểm tra lại. Các kỹ năng phụ thuộc cần đợi kết quả đạt; phần độc lập vẫn học được.";
            }
        }
        for (var pass = 0; pass < statuses.Count; pass++)
            foreach (var skill in statuses)
                if (skill.PrerequisiteSkillIds.Any(id => statuses.Any(s => s.SkillId == id && (s.StageGateFailed || s.BlockedByStageGate))))
                    skill.BlockedByStageGate = true;
        foreach (var skill in statuses.Where(s => s.Availability == "Available" && s.State != "Confirmed"))
            if (skill.PrerequisiteSkillIds.Any(p => statuses.All(s => s.SkillId != p || s.State != "Confirmed") && externalMastery.GetValueOrDefault(p) < 60m))
                skill.Availability = "PrerequisiteBlocked";
        return new CapabilityMapDto
        {
            SubjectId = subject, StudentId = student,
            SubjectName = await db.Subjects.Where(s => s.CenterId == center && s.SubjectId == subject).Select(s => s.SubjectName).SingleAsync(ct),
            Skills = statuses, LastUpdatedAt = now, ExternalPrerequisiteMastery = externalMastery,
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
