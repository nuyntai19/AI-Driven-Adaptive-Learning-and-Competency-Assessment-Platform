using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.KnowledgeGraph;

public sealed record GraphVersionOperation(KnowledgeGraphVersionDto? Data, string? ErrorCode = null, string? Message = null);

/// <summary>Author a draft, then publish an immutable topology. Forking never changes canonical node/edge identities.</summary>
public sealed class KnowledgeGraphVersionService(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock)
{
    private bool Actor(bool write = false) => tenant.IsResolved && tenant.CenterId.HasValue && tenant.CenterId != Guid.Empty &&
        tenant.UserId.HasValue && tenant.UserId != Guid.Empty && (tenant.Role == nameof(UserRole.Teacher) ||
        !write && (tenant.Role == nameof(UserRole.Student) || tenant.Role == nameof(UserRole.CenterManager)));

    private Task<bool> Active(CancellationToken ct) => db.Centers.AnyAsync(c => c.CenterId == tenant.CenterId && !c.IsDeleted && c.Status == CenterStatus.Active, ct);

    private async Task<bool> CanRead(KnowledgeGraphVersion version, CancellationToken ct)
    {
        if (!version.IsLegacy) return version.Status == "Published" ||
            tenant.Role == nameof(UserRole.Teacher) && version.CreatedBy == tenant.UserId;
        return tenant.Role == nameof(UserRole.Teacher) && await db.Curriculums.AnyAsync(c => c.CenterId == tenant.CenterId &&
            c.GraphVersionId == version.GraphVersionId && (c.TeacherId == tenant.UserId || c.Visibility == MaterialVisibility.Shared && c.ReviewStatus == ReviewStatus.Published), ct);
    }

    public async Task<IReadOnlyList<KnowledgeGraphVersionDto>?> ListAsync(Guid subjectId, byte? gradeLevel, CancellationToken ct)
    {
        if (!Actor() || !await Active(ct) || subjectId == Guid.Empty || gradeLevel.HasValue && gradeLevel is < 10 or > 12) return null;
        if (tenant.Role == nameof(UserRole.Teacher) && !await TeachesSubject(subjectId, ct)) return null;
        var versions = await db.KnowledgeGraphVersions.AsNoTracking().Where(v => v.CenterId == tenant.CenterId && v.SubjectId == subjectId &&
            (!gradeLevel.HasValue || v.GradeLevel == gradeLevel)).OrderByDescending(v => v.CreatedAt).ToListAsync(ct);
        var list = new List<KnowledgeGraphVersionDto>();
        StudentLearningScopeResult? learning = tenant.Role == nameof(UserRole.Student)
            ? await StudentLearningScope.ResolveAsync(db, tenant.CenterId!.Value, tenant.UserId!.Value, subjectId, null, false, ct) : null;
        foreach (var version in versions)
            if ((learning == null || learning.GraphVersionIds.Contains(version.GraphVersionId)) && await CanRead(version, ct))
                list.Add(await Project(version, ct));
        return list;
    }

    public async Task<GraphVersionOperation> CreateAsync(CreateKnowledgeGraphVersionRequest request, CancellationToken ct)
    {
        if (!Actor(true) || !await Active(ct)) return new(null, ErrorCodes.ResourceNotFound);
        if (request.SubjectId == Guid.Empty || request.GradeLevel is < 10 or > 12 || request.Title?.Length > 250)
            return new(null, ErrorCodes.ValidationFailed);
        if (!await db.Subjects.AnyAsync(s => s.CenterId == tenant.CenterId && s.SubjectId == request.SubjectId && !s.IsDeleted, ct))
            return new(null, ErrorCodes.ResourceNotFound);
        if (!await TeachesSubject(request.SubjectId, ct)) return new(null, ErrorCodes.ResourceNotFound);
        KnowledgeGraphVersion? source = null;
        if (request.SourceGraphVersionId.HasValue)
        {
            source = await db.KnowledgeGraphVersions.AsNoTracking().SingleOrDefaultAsync(v => v.CenterId == tenant.CenterId && v.GraphVersionId == request.SourceGraphVersionId, ct);
            if (source == null || source.SubjectId != request.SubjectId || !await CanRead(source, ct)) return new(null, ErrorCodes.ResourceNotFound);
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var version = new KnowledgeGraphVersion { GraphVersionId = Guid.NewGuid(), CenterId = tenant.CenterId!.Value, SubjectId = request.SubjectId,
            GradeLevel = request.GradeLevel, Title = request.Title?.Trim(), SourceGraphVersionId = source?.GraphVersionId,
            CreatedAt = now, UpdatedAt = now, CreatedBy = tenant.UserId, UpdatedBy = tenant.UserId };
        await using var transaction = await GraphMutationTransaction.BeginAsync(db, version.CenterId, version.SubjectId, ct);
        if (source != null)
        {
            var topology = await Project(source, ct);
            var invalid = await Validate(version, new() { NodeMappings = topology.NodeMappings, EdgeMappings = topology.EdgeMappings }, ct);
            if (invalid != null) return new(null, ErrorCodes.ValidationFailed, invalid);
        }
        db.KnowledgeGraphVersions.Add(version);
        await db.SaveChangesAsync(ct);
        if (source != null)
        {
            var nodes = await db.KnowledgeNodeGrades.AsNoTracking().Where(n => n.CenterId == version.CenterId && n.GraphVersionId == source.GraphVersionId).ToListAsync(ct);
            foreach (var node in nodes) db.KnowledgeNodeGrades.Add(new() { CenterId = version.CenterId, GraphVersionId = version.GraphVersionId,
                NodeId = node.NodeId, CreatedAt = now, CreatedBy = tenant.UserId });
            await db.SaveChangesAsync(ct);
            foreach (var node in nodes.Where(n => n.ParentNodeId.HasValue))
                db.KnowledgeNodeGrades.Local.Single(n => n.GraphVersionId == version.GraphVersionId && n.NodeId == node.NodeId).ParentNodeId = node.ParentNodeId;
            var edges = await db.KnowledgeEdgeGrades.AsNoTracking().Where(e => e.CenterId == version.CenterId && e.GraphVersionId == source.GraphVersionId).ToListAsync(ct);
            foreach (var edge in edges) db.KnowledgeEdgeGrades.Add(new() { CenterId = version.CenterId, GraphVersionId = version.GraphVersionId,
                EdgeId = edge.EdgeId, RelationType = edge.RelationType, Weight = edge.Weight, SourceGraphVersionId = edge.SourceGraphVersionId,
                CreatedAt = now, CreatedBy = tenant.UserId });
            await db.SaveChangesAsync(ct);
        }
        if (transaction != null) await transaction.CommitAsync(ct);
        return new(await Project(version, ct));
    }

    private Task<bool> TeachesSubject(Guid subjectId, CancellationToken ct) => db.Classes.AnyAsync(c =>
        c.CenterId == tenant.CenterId && c.TeacherId == tenant.UserId && c.SubjectId == subjectId && !c.IsDeleted, ct);

    public async Task<GraphVersionOperation> UpdateAsync(Guid id, UpdateKnowledgeGraphVersionRequest request, CancellationToken ct)
    {
        if (!Actor(true) || !await Active(ct)) return new(null, ErrorCodes.ResourceNotFound);
        if (!ulong.TryParse(request.RowVersion, out var rowVersion) || rowVersion == 0 || request.NodeMappings == null || request.EdgeMappings == null)
            return new(null, ErrorCodes.ValidationFailed);
        var version = await db.KnowledgeGraphVersions.SingleOrDefaultAsync(v => v.CenterId == tenant.CenterId && v.GraphVersionId == id, ct);
        if (version == null || version.CreatedBy != tenant.UserId) return new(null, ErrorCodes.ResourceNotFound);
        await using var transaction = await GraphMutationTransaction.BeginAsync(db, version.CenterId, version.SubjectId, ct);
        if (transaction != null) await db.Entry(version).ReloadAsync(ct);
        if (version.RowVersion != rowVersion) return new(null, ErrorCodes.ConcurrencyConflict);
        if (await Frozen(version, ct)) return new(null, ErrorCodes.InvalidStateTransition, "Phiên bản đã được chốt. Hãy tạo bản nháp từ phiên bản này.");
        var validation = await Validate(version, request, ct);
        if (validation != null) return new(null, ErrorCodes.ValidationFailed, validation);
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            var oldEdges = await db.KnowledgeEdgeGrades.Where(e => e.CenterId == version.CenterId && e.GraphVersionId == id).ToListAsync(ct);
            db.KnowledgeEdgeGrades.RemoveRange(oldEdges);
            var oldNodes = await db.KnowledgeNodeGrades.Where(n => n.CenterId == version.CenterId && n.GraphVersionId == id).ToListAsync(ct);
            foreach (var node in oldNodes) node.ParentNodeId = null;
            await db.SaveChangesAsync(ct);
            db.KnowledgeNodeGrades.RemoveRange(oldNodes);
            await db.SaveChangesAsync(ct);
            foreach (var node in request.NodeMappings) db.KnowledgeNodeGrades.Add(new() { CenterId = version.CenterId, GraphVersionId = id,
                NodeId = ulong.Parse(node.NodeId), CreatedAt = now, CreatedBy = tenant.UserId });
            await db.SaveChangesAsync(ct);
            foreach (var node in request.NodeMappings.Where(n => n.ParentNodeId != null))
                db.KnowledgeNodeGrades.Local.Single(n => n.GraphVersionId == id && n.NodeId == ulong.Parse(node.NodeId)).ParentNodeId = ulong.Parse(node.ParentNodeId!);
            foreach (var edge in request.EdgeMappings) db.KnowledgeEdgeGrades.Add(new() { CenterId = version.CenterId, GraphVersionId = id,
                EdgeId = ulong.Parse(edge.EdgeId), RelationType = Enum.Parse<RelationType>(edge.RelationType), Weight = edge.Weight,
                SourceGraphVersionId = edge.SourceGraphVersionId, CreatedAt = now, CreatedBy = tenant.UserId });
            version.UpdatedAt = now; version.UpdatedBy = tenant.UserId;
            // Topology can change even when the clock/actor matches the previous write.
            // Force a version-row update so optimistic concurrency covers every mapping replacement.
            version.RowVersion = checked(rowVersion + 1);
            await db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return new(null, ErrorCodes.ConcurrencyConflict); }
        return new(await Project(version, ct));
    }

    public async Task<GraphVersionOperation> PublishAsync(Guid id, PublishKnowledgeGraphVersionRequest request, CancellationToken ct)
    {
        if (!Actor(true) || !await Active(ct)) return new(null, ErrorCodes.ResourceNotFound);
        if (!ulong.TryParse(request.RowVersion, out var rowVersion) || rowVersion == 0) return new(null, ErrorCodes.ValidationFailed);
        var version = await db.KnowledgeGraphVersions.SingleOrDefaultAsync(v => v.CenterId == tenant.CenterId && v.GraphVersionId == id, ct);
        if (version == null || version.CreatedBy != tenant.UserId) return new(null, ErrorCodes.ResourceNotFound);
        await using var transaction = await GraphMutationTransaction.BeginAsync(db, version.CenterId, version.SubjectId, ct);
        if (transaction != null) await db.Entry(version).ReloadAsync(ct);
        if (version.RowVersion != rowVersion) return new(null, ErrorCodes.ConcurrencyConflict);
        if (await Frozen(version, ct) || version.IsLegacy) return new(null, ErrorCodes.InvalidStateTransition);
        var projected = await Project(version, ct);
        if (projected.NodeCount == 0) return new(null, ErrorCodes.ValidationFailed, "Phiên bản cần ít nhất một điểm tri thức đã phân loại.");
        var invalid = await Validate(version, new() { NodeMappings = projected.NodeMappings, EdgeMappings = projected.EdgeMappings }, ct);
        if (invalid != null) return new(null, ErrorCodes.ValidationFailed, invalid);
        var previous = await db.KnowledgeGraphVersions.Where(v => v.CenterId == version.CenterId && v.SubjectId == version.SubjectId &&
            v.GradeLevel == version.GradeLevel && v.IsCurrent && v.GraphVersionId != id).ToListAsync(ct);
        foreach (var old in previous)
        {
            old.IsCurrent = false; old.UpdatedAt = clock.GetUtcNow().UtcDateTime; old.UpdatedBy = tenant.UserId;
        }
        await db.SaveChangesAsync(ct);
        version.Status = "Published"; version.IsCurrent = true; version.UpdatedAt = clock.GetUtcNow().UtcDateTime; version.UpdatedBy = tenant.UserId;
        await db.SaveChangesAsync(ct);
        if (transaction != null) await transaction.CommitAsync(ct);
        return new(await Project(version, ct));
    }

    private async Task<bool> Frozen(KnowledgeGraphVersion v, CancellationToken ct) => v.Status != "Draft" || v.IsLegacy ||
        await db.Curriculums.AnyAsync(c => c.CenterId == v.CenterId && c.GraphVersionId == v.GraphVersionId &&
            (c.ReviewStatus == ReviewStatus.Published || c.ReviewStatus == ReviewStatus.Archived ||
             db.ClassCurriculumApplications.Any(a => a.CenterId == c.CenterId && a.CurriculumId == c.CurriculumId)), ct);

    private async Task<string?> Validate(KnowledgeGraphVersion version, UpdateKnowledgeGraphVersionRequest request, CancellationToken ct)
    {
        var nodes = new Dictionary<ulong, ulong?>();
        foreach (var node in request.NodeMappings)
        {
            if (!ulong.TryParse(node.NodeId, out var id) || id == 0 || node.ParentNodeId != null && (!ulong.TryParse(node.ParentNodeId, out var parent) || parent == 0) || nodes.ContainsKey(id))
                return "Mã điểm tri thức hoặc nút cha không hợp lệ/trùng lặp.";
            nodes[id] = node.ParentNodeId == null ? null : ulong.Parse(node.ParentNodeId);
        }
        if (nodes.Values.Any(p => p.HasValue && !nodes.ContainsKey(p.Value))) return "Nút cha phải thuộc cùng phiên bản và khối.";
        var validNodes = await db.KnowledgeNodes.Where(n => n.CenterId == version.CenterId && n.SubjectId == version.SubjectId && !n.IsDeleted && n.IsActive)
            .Select(n => n.NodeId).ToListAsync(ct);
        if (nodes.Keys.Any(n => !validNodes.Contains(n))) return "Điểm tri thức phải đang hoạt động và thuộc đúng môn/trung tâm.";
        foreach (var id in nodes.Keys)
        {
            var seen = new HashSet<ulong>(); ulong? current = id;
            while (current.HasValue) { if (!seen.Add(current.Value)) return "Cây chủ đề có chu trình."; current = nodes[current.Value]; }
        }
        var edges = await db.KnowledgeEdges.Where(e => e.CenterId == version.CenterId && e.SubjectId == version.SubjectId && !e.IsDeleted).ToListAsync(ct);
        var ids = new HashSet<ulong>(); var prerequisites = new Dictionary<ulong, List<ulong>>();
        foreach (var relation in request.EdgeMappings)
        {
            if (!ulong.TryParse(relation.EdgeId, out var id) || !ids.Add(id) || !Enum.TryParse<RelationType>(relation.RelationType, out var type) ||
                !Enum.IsDefined(type) || relation.RelationType != type.ToString() || relation.Weight is < 0 or > 1)
                return "Quan hệ tri thức không hợp lệ/trùng lặp.";
            var edge = edges.SingleOrDefault(e => e.EdgeId == id);
            if (edge == null || !nodes.ContainsKey(edge.TargetNodeId)) return "Đích của quan hệ phải thuộc phiên bản.";
            if (relation.SourceGraphVersionId.HasValue)
            {
                var source = await db.KnowledgeGraphVersions.AsNoTracking().SingleOrDefaultAsync(v => v.CenterId == version.CenterId &&
                    v.GraphVersionId == relation.SourceGraphVersionId && v.SubjectId == version.SubjectId && v.Status == "Published" && !v.IsLegacy, ct);
                if (type != RelationType.PrerequisiteOf || source == null || source.GradeLevel >= version.GradeLevel ||
                    !await db.KnowledgeNodeGrades.AnyAsync(n => n.CenterId == version.CenterId && n.GraphVersionId == source.GraphVersionId && n.NodeId == edge.SourceNodeId, ct))
                    return "Tiên quyết ngoài khối phải chỉ rõ phiên bản đã duyệt ở khối thấp hơn chứa nút nguồn.";
            }
            else if (!nodes.ContainsKey(edge.SourceNodeId)) return "Nguồn quan hệ phải thuộc phiên bản hoặc khai báo tiên quyết ngoài khối.";
            if ((type == RelationType.PrerequisiteOf || type == RelationType.PartOf) && !relation.SourceGraphVersionId.HasValue)
            {
                if (!prerequisites.TryGetValue(edge.SourceNodeId, out var targets)) prerequisites[edge.SourceNodeId] = targets = [];
                targets.Add(edge.TargetNodeId);
            }
        }
        var visiting = new HashSet<ulong>(); var done = new HashSet<ulong>();
        bool Cycle(ulong id) { if (done.Contains(id)) return false; if (!visiting.Add(id)) return true;
            if (prerequisites.TryGetValue(id, out var next) && next.Any(Cycle)) return true; visiting.Remove(id); done.Add(id); return false; }
        return nodes.Keys.Any(Cycle) ? "Quan hệ tiên quyết có chu trình." : null;
    }

    private async Task<KnowledgeGraphVersionDto> Project(KnowledgeGraphVersion v, CancellationToken ct)
    {
        var nodes = await db.KnowledgeNodeGrades.AsNoTracking().Where(n => n.CenterId == v.CenterId && n.GraphVersionId == v.GraphVersionId).OrderBy(n => n.NodeId)
            .Select(n => new KnowledgeNodeMappingDto { NodeId = n.NodeId.ToString(), ParentNodeId = n.ParentNodeId.HasValue ? n.ParentNodeId.Value.ToString() : null }).ToListAsync(ct);
        var edges = await db.KnowledgeEdgeGrades.AsNoTracking().Where(e => e.CenterId == v.CenterId && e.GraphVersionId == v.GraphVersionId).OrderBy(e => e.EdgeId)
            .Select(e => new KnowledgeEdgeMappingDto { EdgeId = e.EdgeId.ToString(), RelationType = e.RelationType.ToString(), Weight = e.Weight, SourceGraphVersionId = e.SourceGraphVersionId }).ToListAsync(ct);
        return new() { GraphVersionId = v.GraphVersionId.ToString("D"), CreatedBy = v.CreatedBy?.ToString("D"), SubjectId = v.SubjectId.ToString("D"), GradeLevel = v.GradeLevel,
            Status = v.Status, IsCurrent = v.IsCurrent, IsLegacy = v.IsLegacy, Title = v.Title, SourceGraphVersionId = v.SourceGraphVersionId?.ToString("D"),
            RowVersion = v.RowVersion.ToString(), NodeCount = nodes.Count, EdgeCount = edges.Count, NodeMappings = nodes, EdgeMappings = edges };
    }
}
