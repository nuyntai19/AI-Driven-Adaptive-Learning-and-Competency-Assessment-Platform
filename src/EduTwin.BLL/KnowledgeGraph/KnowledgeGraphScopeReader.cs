using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.KnowledgeGraph;

internal sealed class GraphScopeException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>Projects immutable version occurrences. A class union must not discard a node's grade/parent provenance.</summary>
internal static class KnowledgeGraphScopeReader
{
    private sealed record Selection(KnowledgeGraphVersion Version, Curriculum? Curriculum, ClassCurriculumApplication? Application = null);

    public static async Task<KnowledgeGraphDto> ReadAsync(EduTwinDbContext db, ITenantContext tenant,
        KnowledgeGraphQuery query, CancellationToken ct)
    {
        var center = tenant.CenterId!.Value;
        var actor = tenant.UserId!.Value;
        var student = tenant.Role == nameof(UserRole.Student);
        var teacher = tenant.Role == nameof(UserRole.Teacher);
        if (!student && !teacher && (query.ClassId.HasValue || query.CurriculumId.HasValue))
            throw new GraphScopeException(ErrorCodes.ResourceNotFound);
        if (query.History && !query.ClassId.HasValue && !query.CurriculumId.HasValue ||
            query.AcademicYear != null && !query.ClassId.HasValue)
            throw new GraphScopeException(ErrorCodes.ValidationFailed);
        var scope = new KnowledgeGraphResolvedScope { History = query.History, GradeLevel = query.GradeLevel,
            ReadOnly = student || query.History, AcademicYear = query.AcademicYear };
        if (query.GradeLevel.HasValue && query.GradeLevel is < 10 or > 12 || query.ClassId == Guid.Empty ||
            query.CurriculumId == Guid.Empty || query.GraphVersionId == Guid.Empty ||
            query.ClassId.HasValue && query.CurriculumId.HasValue)
            throw new GraphScopeException(ErrorCodes.ValidationFailed);
        Guid subjectId = query.SubjectId;
        var selections = new List<Selection>();
        if (query.ClassId.HasValue)
        {
            scope.Kind = "Class";
            var klass = await db.Classes.AsNoTracking().SingleOrDefaultAsync(c => c.CenterId == center && c.ClassId == query.ClassId, ct)
                ?? throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (teacher && klass.TeacherId != actor || student && !await db.ClassStudents.AnyAsync(m =>
                m.CenterId == center && m.ClassId == klass.ClassId && m.StudentId == actor &&
                (query.History || m.Status == ClassStudentStatus.Active), ct))
                throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (query.AcademicYear != null && query.AcademicYear != klass.AcademicYear ||
                query.GradeLevel.HasValue && query.GradeLevel != klass.GradeLevel ||
                subjectId != Guid.Empty && subjectId != klass.SubjectId)
                throw new GraphScopeException(ErrorCodes.ValidationFailed);
            subjectId = klass.SubjectId;
            scope.ClassId = klass.ClassId.ToString("D"); scope.AcademicYear = klass.AcademicYear;
            scope.GradeLevel = klass.GradeLevel;
            if (!query.History && (klass.Status != ClassStatus.Active || klass.LearningScope != ClassLearningScope.Current))
                scope.Message = "Lớp đã lưu trữ. Chọn lịch sử để xem giáo trình trước đây.";
            else
            {
                var applications = await db.ClassCurriculumApplications.AsNoTracking()
                    .Where(a => a.CenterId == center && a.ClassId == klass.ClassId && (query.History || a.EndedAt == null))
                    .OrderBy(a => a.StartedAt).ToListAsync(ct);
                var curricula = await db.Curriculums.AsNoTracking().Where(c => c.CenterId == center &&
                    c.SubjectId == subjectId && (query.History ? c.ReviewStatus != ReviewStatus.Draft : c.ReviewStatus == ReviewStatus.Published))
                    .ToListAsync(ct);
                foreach (var application in applications)
                {
                    var curriculum = curricula.SingleOrDefault(c => c.CurriculumId == application.CurriculumId);
                    if (curriculum == null) continue;
                    if (curriculum.GraphVersionId.HasValue)
                    {
                        var version = await db.KnowledgeGraphVersions.AsNoTracking().SingleOrDefaultAsync(v =>
                            v.CenterId == center && v.GraphVersionId == curriculum.GraphVersionId, ct);
                        if (version != null) selections.Add(new(version, curriculum, application));
                    }
                }
                if (selections.Count == 0) scope.Message = "Lớp chưa có giáo trình với phiên bản khung tri thức được áp dụng. Không dùng toàn môn thay thế.";
            }
        }
        else if (query.CurriculumId.HasValue)
        {
            scope.Kind = "Curriculum";
            var curriculum = await db.Curriculums.AsNoTracking().SingleOrDefaultAsync(c => c.CenterId == center && c.CurriculumId == query.CurriculumId, ct)
                ?? throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            var canRead = teacher && (curriculum.TeacherId == actor || curriculum.Visibility == MaterialVisibility.Shared && curriculum.ReviewStatus == ReviewStatus.Published);
            if (student)
                canRead = await (from m in db.ClassStudents join a in db.ClassCurriculumApplications
                    on new { m.CenterId, m.ClassId } equals new { a.CenterId, a.ClassId }
                    where m.CenterId == center && m.StudentId == actor && a.CurriculumId == curriculum.CurriculumId &&
                        (query.History || m.Status == ClassStudentStatus.Active && a.EndedAt == null && m.Class.Status == ClassStatus.Active)
                    select a.ApplicationId).AnyAsync(ct);
            if (!canRead) throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (subjectId != Guid.Empty && subjectId != curriculum.SubjectId || query.GradeLevel.HasValue && query.GradeLevel != curriculum.GradeLevel ||
                query.GraphVersionId.HasValue && query.GraphVersionId != curriculum.GraphVersionId || query.AcademicYear != null)
                throw new GraphScopeException(ErrorCodes.ValidationFailed);
            subjectId = curriculum.SubjectId; scope.GradeLevel = curriculum.GradeLevel;
            scope.CurriculumId = curriculum.CurriculumId.ToString("D");
            scope.ReadOnly |= curriculum.ReviewStatus != ReviewStatus.Draft || curriculum.TeacherId != actor;
            if (curriculum.GraphVersionId.HasValue)
            {
                var version = await db.KnowledgeGraphVersions.AsNoTracking().SingleOrDefaultAsync(v =>
                    v.CenterId == center && v.GraphVersionId == curriculum.GraphVersionId, ct);
                if (version != null) selections.Add(new(version, curriculum));
            }
            if (selections.Count == 0) scope.Message = "Giáo trình chưa chọn phiên bản khung tri thức được phân loại cho khối này.";
        }
        else if (query.GraphVersionId.HasValue)
        {
            scope.Kind = "Version";
            var version = await db.KnowledgeGraphVersions.AsNoTracking().SingleOrDefaultAsync(v => v.CenterId == center && v.GraphVersionId == query.GraphVersionId, ct)
                ?? throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (student && version.Status != "Published" || version.IsLegacy && !teacher)
                throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (version.Status == "Draft" && version.CreatedBy != actor)
                throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (version.IsLegacy && !await db.Curriculums.AnyAsync(c => c.CenterId == center && c.GraphVersionId == version.GraphVersionId &&
                (c.TeacherId == actor || c.Visibility == MaterialVisibility.Shared && c.ReviewStatus == ReviewStatus.Published), ct))
                throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (subjectId != Guid.Empty && subjectId != version.SubjectId || query.GradeLevel.HasValue && query.GradeLevel != version.GradeLevel || query.AcademicYear != null)
                throw new GraphScopeException(ErrorCodes.ValidationFailed);
            subjectId = version.SubjectId; scope.GradeLevel = version.GradeLevel;
            selections.Add(new(version, null));
        }
        else if (query.GradeLevel.HasValue)
        {
            scope.Kind = "Grade";
            scope.GradeLevel = query.GradeLevel;
            var version = await db.KnowledgeGraphVersions.AsNoTracking().SingleOrDefaultAsync(v => v.CenterId == center &&
                v.SubjectId == subjectId && v.GradeLevel == scope.GradeLevel && v.Status == "Published" && v.IsCurrent && !v.IsLegacy, ct);
            if (version != null) selections.Add(new(version, null));
            else scope.Message = "Khối này chưa có phiên bản khung tri thức đã duyệt. Hãy phân loại và xuất bản phiên bản trước.";
        }
        else if (student)
        {
            scope.Kind = "Grade";
            var profile = await db.Students.AsNoTracking().SingleOrDefaultAsync(s => s.CenterId == center && s.StudentId == actor, ct);
            if (profile?.GradeLevel is >= 10 and <= 12)
            {
                scope.Kind = "Grade";
                scope.GradeLevel = profile.GradeLevel;
                var version = await db.KnowledgeGraphVersions.AsNoTracking().SingleOrDefaultAsync(v => v.CenterId == center &&
                    v.SubjectId == subjectId && v.GradeLevel == scope.GradeLevel && v.Status == "Published" && v.IsCurrent && !v.IsLegacy, ct);
                if (version != null) selections.Add(new(version, null));
                else scope.Message = "Khối này chưa có phiên bản khung tri thức đã duyệt. Hãy phân loại và xuất bản phiên bản trước.";
            }
        }
        else if (query.AcademicYear != null || query.History)
            throw new GraphScopeException(ErrorCodes.ValidationFailed);

        if (subjectId == Guid.Empty) throw new GraphScopeException(ErrorCodes.ValidationFailed);
        if (!await db.Subjects.AnyAsync(s => s.CenterId == center && s.SubjectId == subjectId && !s.IsDeleted, ct))
            throw new GraphScopeException(ErrorCodes.ResourceNotFound);
        HashSet<ulong>? allowedStudentIds = null;
        if (student && !query.History)
        {
            var learningScope = await StudentLearningScope.ResolveAsync(db, center, actor, subjectId, query.ClassId, false, ct);
            allowedStudentIds = learningScope.TopicIds ?? [];
            if (query.GraphVersionId.HasValue && !learningScope.GraphVersionIds.Contains(query.GraphVersionId.Value))
                throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (query.GradeLevel.HasValue && !await db.KnowledgeGraphVersions.AnyAsync(v => v.CenterId == center &&
                learningScope.GraphVersionIds.Contains(v.GraphVersionId) && v.GradeLevel == query.GradeLevel, ct))
                throw new GraphScopeException(ErrorCodes.ResourceNotFound);
            if (!learningScope.Allowed)
            {
                selections.Clear(); scope.Message = learningScope.Reason;
            }
            else if (!query.ClassId.HasValue && !query.CurriculumId.HasValue && !query.GraphVersionId.HasValue)
            {
                selections.Clear();
                var applications = await (from membership in db.ClassStudents
                    join application in db.ClassCurriculumApplications on new { membership.CenterId, membership.ClassId } equals new { application.CenterId, application.ClassId }
                    where membership.CenterId == center && membership.StudentId == actor && membership.Status == ClassStudentStatus.Active &&
                        application.EndedAt == null && membership.Class.Status == ClassStatus.Active && membership.Class.LearningScope == ClassLearningScope.Current
                    select application.CurriculumId).Distinct().ToListAsync(ct);
                var curricula = await db.Curriculums.AsNoTracking().Where(c => c.CenterId == center && c.SubjectId == subjectId && c.ReviewStatus == ReviewStatus.Published).ToListAsync(ct);
                foreach (var id in learningScope.GraphVersionIds)
                {
                    var version = await db.KnowledgeGraphVersions.AsNoTracking().SingleAsync(v => v.CenterId == center && v.GraphVersionId == id, ct);
                    var applicable = curricula.Where(c => c.GraphVersionId == id && applications.Contains(c.CurriculumId)).ToList();
                    if (applicable.Count > 0) selections.AddRange(applicable.Select(c => new Selection(version, c)));
                    else selections.Add(new(version, null));
                }
                scope.Message = null;
                if (query.GradeLevel.HasValue) selections.RemoveAll(s => s.Version.GradeLevel != query.GradeLevel.Value);
            }
        }
        if (query.ClassId.HasValue && query.GraphVersionId.HasValue && !selections.Any(s => s.Version.GraphVersionId == query.GraphVersionId))
            throw new GraphScopeException(ErrorCodes.ValidationFailed);
        if (query.ClassId.HasValue && query.GraphVersionId.HasValue)
            selections.RemoveAll(s => s.Version.GraphVersionId != query.GraphVersionId);
        scope.SubjectId = subjectId.ToString("D");
        scope.IsLegacy = selections.Any(s => s.Version.IsLegacy);
        scope.ReadOnly |= selections.Any(s => s.Version.Status != "Draft" || s.Version.IsLegacy);
        if (selections.Select(s => s.Version.GraphVersionId).Distinct().Count() == 1)
            scope.GraphVersionId = selections[0].Version.GraphVersionId.ToString("D");
        var allNodes = await db.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == center && n.SubjectId == subjectId && !n.IsDeleted)
            .OrderBy(n => n.OrderIndex).ThenBy(n => n.NodeId).ToListAsync(ct);
        var allEdges = await db.KnowledgeEdges.AsNoTracking().Where(e => e.CenterId == center && e.SubjectId == subjectId && !e.IsDeleted).ToListAsync(ct);
        var resultNodes = new Dictionary<ulong, KnowledgeGraphNodeDto>();
        var provenance = new Dictionary<ulong, List<KnowledgeNodeProvenanceDto>>();
        var internalEdges = new List<KnowledgeGraphEdgeDto>();
        var external = new List<KnowledgeGraphEdgeDto>();
        foreach (var selection in selections)
        {
            var version = selection.Version;
            var mappings = await db.KnowledgeNodeGrades.AsNoTracking().Where(n => n.CenterId == center && n.GraphVersionId == version.GraphVersionId).ToListAsync(ct);
            HashSet<ulong> selected = mappings.Select(n => n.NodeId).ToHashSet();
            if (allowedStudentIds != null) selected.IntersectWith(allowedStudentIds);
            if (selection.Curriculum != null)
                selected.IntersectWith(await db.CurriculumNodes.AsNoTracking().Where(n => n.CenterId == center && n.CurriculumId == selection.Curriculum.CurriculumId).Select(n => n.NodeId).ToListAsync(ct));
            string SourceKey() => selection.Application?.ApplicationId.ToString("D") ?? selection.Curriculum?.CurriculumId.ToString("D") ?? "library";
            string Occurrence(ulong id) => $"{version.GraphVersionId:D}:{SourceKey()}:{id}";
            foreach (var mapping in mappings.Where(n => selected.Contains(n.NodeId)))
            {
                var node = allNodes.FirstOrDefault(n => n.NodeId == mapping.NodeId);
                if (node == null) continue;
                resultNodes.TryAdd(node.NodeId, Node(node));
                if (!provenance.TryGetValue(node.NodeId, out var sources)) provenance[node.NodeId] = sources = [];
                sources.Add(new() { OccurrenceId = Occurrence(node.NodeId), NodeId = node.NodeId.ToString(), GraphVersionId = version.GraphVersionId.ToString("D"),
                    GradeLevel = version.GradeLevel, CurriculumId = selection.Curriculum?.CurriculumId.ToString("D"),
                    CurriculumTitle = selection.Curriculum?.Title, ApplicationId = selection.Application?.ApplicationId.ToString("D"),
                    ApplicationRole = selection.Application?.ApplicationRole, IsGradeException = selection.Application?.IsGradeException ?? false,
                    GradeMismatchReason = selection.Application?.GradeMismatchReason,
                    ParentNodeId = mapping.ParentNodeId.HasValue && selected.Contains(mapping.ParentNodeId.Value) ? mapping.ParentNodeId.Value.ToString() : null });
            }
            var relations = await db.KnowledgeEdgeGrades.AsNoTracking().Where(e => e.CenterId == center && e.GraphVersionId == version.GraphVersionId).ToListAsync(ct);
            foreach (var relation in relations)
            {
                var edge = allEdges.FirstOrDefault(e => e.EdgeId == relation.EdgeId);
                if (edge == null || !selected.Contains(edge.TargetNodeId)) continue;
                var isExternal = relation.SourceGraphVersionId.HasValue || !selected.Contains(edge.SourceNodeId);
                if (isExternal && relation.RelationType != RelationType.PrerequisiteOf) continue;
                var dto = Edge(edge);
                dto.RelationType = relation.RelationType.ToString(); dto.Weight = relation.Weight;
                dto.OccurrenceId = $"{version.GraphVersionId:D}:{SourceKey()}:edge:{edge.EdgeId}";
                dto.GraphVersionId = version.GraphVersionId.ToString("D"); dto.GradeLevel = version.GradeLevel;
                dto.CurriculumId = selection.Curriculum?.CurriculumId.ToString("D");
                dto.SourceOccurrenceId = isExternal ? null : Occurrence(edge.SourceNodeId); dto.TargetOccurrenceId = Occurrence(edge.TargetNodeId);
                dto.External = isExternal; dto.SourceGraphVersionId = (relation.SourceGraphVersionId ?? version.GraphVersionId).ToString("D");
                dto.SourceNodeName = allNodes.FirstOrDefault(n => n.NodeId == edge.SourceNodeId)?.NodeName;
                dto.SourceGradeLevel = relation.SourceGraphVersionId.HasValue
                    ? await db.KnowledgeGraphVersions.Where(v => v.CenterId == center && v.GraphVersionId == relation.SourceGraphVersionId).Select(v => (byte?)v.GradeLevel).SingleOrDefaultAsync(ct)
                    : version.IsLegacy ? null : version.GradeLevel;
                (isExternal ? external : internalEdges).Add(dto);
            }
        }
        foreach (var (id, sources) in provenance)
        {
            resultNodes[id].Provenance = sources;
            resultNodes[id].GradeLevels = sources.Select(p => p.GradeLevel).Distinct().Order().ToList();
            var parents = sources.Select(p => p.ParentNodeId).Distinct().ToList();
            resultNodes[id].ParentNodeId = parents.Count == 1 ? parents[0] : null;
        }
        if (scope.Kind == "Subject" && !student)
        {
            // Explicit overview for authoring only. Never used as a grade/class picker fallback.
            foreach (var n in allNodes) resultNodes[n.NodeId] = Node(n);
            var applicability = await (from node in db.KnowledgeNodeGrades.AsNoTracking()
                join version in db.KnowledgeGraphVersions.AsNoTracking() on new { node.CenterId, node.GraphVersionId } equals new { version.CenterId, version.GraphVersionId }
                where node.CenterId == center && version.SubjectId == subjectId && version.Status == "Published" && version.IsCurrent && !version.IsLegacy
                select new { node.NodeId, version.GradeLevel }).ToListAsync(ct);
            foreach (var group in applicability.GroupBy(n => n.NodeId))
                if (resultNodes.TryGetValue(group.Key, out var node)) node.GradeLevels = group.Select(n => n.GradeLevel).Distinct().Order().ToList();
            internalEdges.AddRange(allEdges.Select(Edge));
            scope.ReadOnly = true;
            scope.Message = "Tổng quan thư viện môn. Chọn khối hoặc phiên bản để xem phạm vi được áp dụng.";
        }
        static ulong Id(string s) => ulong.Parse(s);
        return new() { SubjectId = scope.SubjectId, ResolvedScope = scope, Nodes = resultNodes.Values.OrderBy(n => n.OrderIndex).ThenBy(n => Id(n.NodeId)).ToList(),
            Edges = internalEdges.OrderBy(e => Id(e.SourceNodeId)).ThenBy(e => Id(e.TargetNodeId)).ThenBy(e => e.RelationType).ThenBy(e => Id(e.EdgeId)).ToList(),
            ExternalPrerequisites = external.OrderBy(e => Id(e.SourceNodeId)).ThenBy(e => Id(e.TargetNodeId)).ThenBy(e => e.RelationType).ThenBy(e => Id(e.EdgeId)).ToList() };
    }

    private static KnowledgeGraphNodeDto Node(KnowledgeNode n) => new() { NodeId = n.NodeId.ToString(), ParentNodeId = n.ParentNodeId?.ToString(),
        NodeType = n.NodeType.ToString(), NodeCode = n.NodeCode, NodeName = n.NodeName, Description = n.Description, OrderIndex = n.OrderIndex,
        ExamImportance = n.ExamImportance, EstimatedLearningMinutes = n.EstimatedLearningMinutes, IsActive = n.IsActive, RowVersion = n.RowVersion.ToString() };
    private static KnowledgeGraphEdgeDto Edge(KnowledgeEdge e) => new() { EdgeId = e.EdgeId.ToString(), SourceNodeId = e.SourceNodeId.ToString(),
        TargetNodeId = e.TargetNodeId.ToString(), RelationType = e.RelationType.ToString(), Weight = e.Weight, RowVersion = e.RowVersion.ToString() };
}
