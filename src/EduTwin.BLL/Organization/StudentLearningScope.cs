using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Organization;

public sealed class LearningScopeDeniedException(string message) : InvalidOperationException(message);

public sealed record StudentLearningScopeResult(bool Allowed, HashSet<ulong>? TopicIds, string? Reason)
{
    public IReadOnlyList<Guid> GraphVersionIds { get; init; } = [];
    public IReadOnlyDictionary<Guid, HashSet<ulong>> NodeIdsByGraphVersionId { get; init; } = new Dictionary<Guid, HashSet<ulong>>();
    public IReadOnlyDictionary<ulong, uint> OrderByNodeId { get; init; } = new Dictionary<ulong, uint>();
    public IReadOnlyDictionary<ulong, HashSet<byte>> QuestionGradesByNodeId { get; init; } = new Dictionary<ulong, HashSet<byte>>();
    public bool Includes(ulong topicId) => Allowed && TopicIds is not null && TopicIds.Contains(topicId);
    public bool IncludesQuestion(ulong topicId, byte? grade) => Includes(topicId) && grade.HasValue &&
        QuestionGradesByNodeId.TryGetValue(topicId, out var grades) && grades.Contains(grade.Value);
}

public static class StudentLearningScope
{
    public const string ReadOnlyReason = "Lớp đã lưu trữ hoặc bạn đã rời lớp. Chỉ được xem lại bài làm; hãy chuyển sang lớp đang học để tiếp tục.";

    public static async Task<StudentLearningScopeResult> ResolveAsync(EduTwinDbContext db, Guid center, Guid student,
        Guid subject, Guid? classId, bool history, CancellationToken ct)
    {
        if (history || classId == Guid.Empty) return new(false, [], ReadOnlyReason);
        var studentGrade = await db.Students.AsNoTracking().Where(s => s.CenterId == center && s.StudentId == student && !s.IsDeleted)
            .Select(s => (byte?)s.GradeLevel).SingleOrDefaultAsync(ct);
        if (!studentGrade.HasValue) return new(false, [], "Không tìm thấy hồ sơ học sinh.");
        var memberships = await StudentClassScope.Memberships(db, center, student).AsNoTracking()
            .Where(m => m.SubjectId == subject).ToListAsync(ct);
        var active = memberships.Where(m => !m.IsHistorical && (!classId.HasValue || m.ClassId == classId)).ToList();
        var nodeGrades = new Dictionary<ulong, HashSet<byte>>();
        var nodeOrders = new Dictionary<ulong, uint>();
        var versionIds = new HashSet<Guid>();
        var versionNodes = new Dictionary<Guid, HashSet<ulong>>();
        if (active.Count == 0)
        {
            if (classId.HasValue || memberships.Count > 0) return new(false, [], ReadOnlyReason);
            // Independent practice uses the student's approved grade library, never all nodes in the subject.
            var versions = await db.KnowledgeGraphVersions.AsNoTracking().Where(v => v.CenterId == center &&
                v.SubjectId == subject && v.GradeLevel == studentGrade && v.Status == "Published" && v.IsCurrent && !v.IsLegacy)
                .Select(v => v.GraphVersionId).ToListAsync(ct);
            foreach (var version in versions)
            {
                versionIds.Add(version);
                var nodes = await db.KnowledgeNodeGrades.AsNoTracking().Where(m => m.CenterId == center && m.GraphVersionId == version)
                    .Select(m => m.NodeId).ToListAsync(ct);
                foreach (var node in nodes) AddGrade(version, node, studentGrade.Value);
            }
        }
        else
        {
            // The application ledger is authoritative. Ended applications and draft plans never become study scope.
            foreach (var membership in active)
            {
                var applications = await db.ClassCurriculumApplications.AsNoTracking().Where(a => a.CenterId == center &&
                    a.ClassId == membership.ClassId && a.EndedAt == null && !a.Curriculum.IsDeleted &&
                    a.Curriculum.SubjectId == subject && a.Curriculum.ReviewStatus == ReviewStatus.Published &&
                    (a.ApplicationRole == "Primary" || a.ApplicationRole == "Supplemental"))
                    .Select(a => new { a.CurriculumId, a.Curriculum.GraphVersionId, a.Curriculum.GradeLevel,
                        a.IsGradeException, a.GradeMismatchReason }).ToListAsync(ct);
                foreach (var application in applications)
                {
                    if (!application.GraphVersionId.HasValue || !application.GradeLevel.HasValue ||
                        (application.GradeLevel != membership.GradeLevel &&
                         (!application.IsGradeException || string.IsNullOrWhiteSpace(application.GradeMismatchReason)))) continue;
                    var version = application.GraphVersionId.Value;
                    if (!await db.KnowledgeGraphVersions.AsNoTracking().AnyAsync(v => v.CenterId == center &&
                        v.GraphVersionId == version && v.SubjectId == subject && v.GradeLevel == application.GradeLevel &&
                        v.Status == "Published", ct)) continue;
                    versionIds.Add(version);
                    var mappedNodes = db.KnowledgeNodeGrades.AsNoTracking().Where(m => m.CenterId == center && m.GraphVersionId == version)
                        .Select(m => m.NodeId);
                    var nodes = await db.CurriculumNodes.AsNoTracking().Where(n => n.CenterId == center &&
                        n.CurriculumId == application.CurriculumId && mappedNodes.Contains(n.NodeId))
                        .Select(n => new { n.NodeId, n.OrderIndex }).ToListAsync(ct);
                    foreach (var node in nodes)
                    {
                        AddGrade(version, node.NodeId, application.GradeLevel.Value);
                        nodeOrders[node.NodeId] = Math.Min(nodeOrders.GetValueOrDefault(node.NodeId, uint.MaxValue), node.OrderIndex);
                    }
                }
            }
        }
        var ids = nodeGrades.Keys.ToList();
        var activeIds = (await db.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == center && n.SubjectId == subject &&
            n.IsActive && !n.IsDeleted && ids.Contains(n.NodeId)).Select(n => n.NodeId).ToListAsync(ct)).ToHashSet();
        foreach (var id in ids.Where(id => !activeIds.Contains(id))) nodeGrades.Remove(id);
        foreach (var members in versionNodes.Values) members.IntersectWith(activeIds);
        return new(activeIds.Count > 0, activeIds, activeIds.Count == 0
            ? active.Count == 0 ? "Khối của bạn chưa có thư viện tri thức đã được duyệt. Không dùng đồ thị toàn môn thay thế."
                : "Lớp đang học chưa có giáo trình với đồ thị đã xuất bản được áp dụng. Không dùng đồ thị toàn môn thay thế."
            : null) { GraphVersionIds = versionIds.Order().ToList(), NodeIdsByGraphVersionId = versionNodes,
                QuestionGradesByNodeId = nodeGrades, OrderByNodeId = nodeOrders };

        void AddGrade(Guid version, ulong node, byte grade)
        {
            if (!nodeGrades.TryGetValue(node, out var grades)) nodeGrades[node] = grades = [];
            grades.Add(grade);
            if (!versionNodes.TryGetValue(version, out var members)) versionNodes[version] = members = [];
            members.Add(node);
        }
    }
}
