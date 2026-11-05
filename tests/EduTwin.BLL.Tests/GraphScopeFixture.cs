using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests;

internal static class GraphScopeFixture
{
    // Test authors declare the grade and IDs explicitly. This is not a production backfill/inference policy.
    internal static Guid Approve(EduTwinDbContext db, Guid center, Guid subject, byte grade, params ulong[] nodes)
    {
        var version = new KnowledgeGraphVersion { CenterId = center, GraphVersionId = Guid.NewGuid(), SubjectId = subject,
            GradeLevel = grade, Status = "Published", IsCurrent = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.KnowledgeGraphVersions.Add(version);
        foreach (var id in nodes.Distinct()) db.KnowledgeNodeGrades.Add(new KnowledgeNodeGrade { CenterId = center,
            GraphVersionId = version.GraphVersionId, NodeId = id,
            ParentNodeId = db.KnowledgeNodes.Local.FirstOrDefault(n => n.CenterId == center && n.NodeId == id)?.ParentNodeId,
            CreatedAt = DateTime.UtcNow });
        return version.GraphVersionId;
    }

    internal static void MapEdges(EduTwinDbContext db, Guid center, Guid version, params ulong[] edgeIds)
    {
        foreach (var id in edgeIds)
        {
            var edge = db.KnowledgeEdges.Local.Single(e => e.CenterId == center && e.EdgeId == id);
            db.KnowledgeEdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = center, GraphVersionId = version,
                EdgeId = id, RelationType = edge.RelationType, Weight = edge.Weight, CreatedAt = DateTime.UtcNow });
        }
    }
}
