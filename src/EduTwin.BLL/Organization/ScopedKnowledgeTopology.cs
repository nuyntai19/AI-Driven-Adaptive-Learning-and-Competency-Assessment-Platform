using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Organization;

internal sealed record ScopedKnowledgeRelation(Guid GraphVersionId, Guid SourceGraphVersionId, KnowledgeEdge Edge);

internal sealed record ScopedKnowledgeTopology(IReadOnlyDictionary<ulong, ulong?> Parents, IReadOnlyList<KnowledgeEdge> Edges)
{
    internal IReadOnlyDictionary<Guid, IReadOnlyDictionary<ulong, ulong?>> Hierarchies { get; init; } =
        new Dictionary<Guid, IReadOnlyDictionary<ulong, ulong?>>();
    internal IReadOnlyList<ScopedKnowledgeRelation> Relations { get; init; } = [];

    internal bool Under(ulong node, ulong ancestor, Guid version)
    {
        if (!Hierarchies.TryGetValue(version, out var parents)) return false;
        var visited = new HashSet<ulong>();
        while (parents.TryGetValue(node, out var parent) && visited.Add(node))
        {
            if (node == ancestor) return true;
            if (!parent.HasValue) break;
            node = parent.Value;
        }
        return false;
    }

    internal static async Task<ScopedKnowledgeTopology> ReadAsync(EduTwinDbContext db, Guid center, Guid subject,
        StudentLearningScopeResult scope, CancellationToken ct)
    {
        var parents = new Dictionary<ulong, ulong?>();
        var edges = new List<KnowledgeEdge>();
        var hierarchies = new Dictionary<Guid, IReadOnlyDictionary<ulong, ulong?>>();
        var scopedRelations = new List<ScopedKnowledgeRelation>();
        foreach (var version in scope.GraphVersionIds)
        {
            var members = await db.KnowledgeNodeGrades.AsNoTracking().Where(m => m.CenterId == center && m.GraphVersionId == version)
                .ToListAsync(ct);
            var membersById = members.ToDictionary(m => m.NodeId);
            hierarchies[version] = members.ToDictionary(m => m.NodeId, m => m.ParentNodeId);
            foreach (var member in members) parents.TryAdd(member.NodeId, member.ParentNodeId);
            var relations = await (from membership in db.KnowledgeEdgeGrades.AsNoTracking()
                join edge in db.KnowledgeEdges.AsNoTracking()
                    on new { membership.CenterId, membership.EdgeId } equals new { edge.CenterId, edge.EdgeId }
                where membership.CenterId == center && membership.GraphVersionId == version && edge.SubjectId == subject && !edge.IsDeleted
                select new { Membership = membership, Edge = edge }).ToListAsync(ct);
            // An explicitly empty relation set is a valid snapshot. Canonical edges are never
            // a fallback: adding an edge later must not rewrite this version's topology.
            foreach (var relation in relations)
            {
                if (!membersById.ContainsKey(relation.Edge.TargetNodeId) ||
                    !scope.NodeIdsByGraphVersionId.TryGetValue(version, out var selected) || !selected.Contains(relation.Edge.TargetNodeId)) continue;
                if (relation.Membership.SourceGraphVersionId is { } sourceVersion)
                {
                    if (relation.Membership.RelationType != RelationType.PrerequisiteOf ||
                        !await db.KnowledgeGraphVersions.AsNoTracking().AnyAsync(v => v.CenterId == center &&
                            v.GraphVersionId == sourceVersion && v.SubjectId == subject && v.Status == "Published", ct)) continue;
                    var sourceMembers = await db.KnowledgeNodeGrades.AsNoTracking().Where(m => m.CenterId == center && m.GraphVersionId == sourceVersion)
                        .ToListAsync(ct);
                    if (sourceMembers.All(m => m.NodeId != relation.Edge.SourceNodeId)) continue;
                    hierarchies[sourceVersion] = sourceMembers.ToDictionary(m => m.NodeId, m => m.ParentNodeId);
                    foreach (var member in sourceMembers) parents.TryAdd(member.NodeId, member.ParentNodeId);
                }
                else if (!membersById.ContainsKey(relation.Edge.SourceNodeId)) continue;
                relation.Edge.RelationType = relation.Membership.RelationType;
                relation.Edge.Weight = relation.Membership.Weight;
                edges.Add(relation.Edge);
                scopedRelations.Add(new(version, relation.Membership.SourceGraphVersionId ?? version, relation.Edge));
            }
        }
        return new(parents, edges.DistinctBy(e => (e.EdgeId, e.RelationType)).ToList())
            { Hierarchies = hierarchies, Relations = scopedRelations };
    }
}
