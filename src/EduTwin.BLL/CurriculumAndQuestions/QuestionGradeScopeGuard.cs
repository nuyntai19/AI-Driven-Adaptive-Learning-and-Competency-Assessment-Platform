using System.Globalization;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

internal static class QuestionGradeScopeGuard
{
    public static async Task<bool> IsValidAsync(EduTwinDbContext db, Guid center, Guid subject, byte? grade,
        ulong primaryNode, IReadOnlyCollection<KnowledgeMappingInput>? mappings, CancellationToken ct)
    {
        if (grade is not (>= 10 and <= 12)) return false;
        var required = new HashSet<ulong> { primaryNode };
        var prerequisites = new HashSet<ulong>();
        var seen = new HashSet<ulong>();
        foreach (var mapping in mappings ?? [])
        {
            if (!ulong.TryParse(mapping.NodeId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0 ||
                !seen.Add(id) || !Enum.TryParse<MappingRole>(mapping.MappingRole, out var role) ||
                !Enum.IsDefined(role) || role.ToString() != mapping.MappingRole) return false;
            if (role == MappingRole.Prerequisite) prerequisites.Add(id);
            else required.Add(id);
        }
        var approved = await ApprovedNodeGradesAsync(db, center, subject, ct);
        var gradeNodes = approved.Where(n => n.GradeLevel == grade).Select(n => n.NodeId).ToHashSet();
        if (!required.IsSubsetOf(gradeNodes)) return false;
        // A prerequisite role explicitly declares a cross-grade dependency; it is never a target-topic escape hatch.
        return prerequisites.IsSubsetOf(approved.Select(n => n.NodeId).ToHashSet());
    }

    internal static async Task<HashSet<(ulong NodeId, byte GradeLevel)>> ApprovedNodeGradesAsync(
        EduTwinDbContext db, Guid center, Guid subject, CancellationToken ct)
    {
        var approved = from membership in db.KnowledgeNodeGrades.AsNoTracking()
            join version in db.KnowledgeGraphVersions.AsNoTracking()
                on new { membership.CenterId, membership.GraphVersionId } equals new { version.CenterId, version.GraphVersionId }
            join node in db.KnowledgeNodes.AsNoTracking()
                on new { membership.CenterId, membership.NodeId } equals new { node.CenterId, node.NodeId }
            where membership.CenterId == center && version.SubjectId == subject && version.Status == "Published" &&
                version.IsCurrent && !version.IsLegacy && node.SubjectId == subject && node.IsActive && !node.IsDeleted
            select new { membership.NodeId, version.GradeLevel };
        return (await approved.ToListAsync(ct)).Select(n => (n.NodeId, n.GradeLevel)).ToHashSet();
    }
}
