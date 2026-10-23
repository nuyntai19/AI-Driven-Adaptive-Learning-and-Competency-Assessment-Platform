using System.Globalization;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

internal sealed record CurriculumGraphScopeResult(Guid? GraphVersionId, CurriculumScopeConflictDto? Conflict);

internal static class CurriculumGraphScopeGuard
{
    public static async Task<CurriculumGraphScopeResult> ResolveAsync(EduTwinDbContext db, Guid center,
        Guid subject, byte grade, Guid? requestedVersion, Guid? existingVersion, IReadOnlyCollection<ulong> nodeIds,
        bool allowUnclassifiedEmptyDraft, CancellationToken ct)
    {
        var versions = db.KnowledgeGraphVersions.AsNoTracking().Where(v => v.CenterId == center &&
            v.SubjectId == subject && v.GradeLevel == grade && v.Status == "Published" && !v.IsLegacy);
        var chosen = requestedVersion ?? existingVersion;
        var version = chosen.HasValue
            ? await versions.SingleOrDefaultAsync(v => v.GraphVersionId == chosen.Value, ct)
            : await versions.SingleOrDefaultAsync(v => v.IsCurrent, ct);
        // An old pin from a different grade never prevents selecting the new grade's approved library.
        if (version is null && !requestedVersion.HasValue && existingVersion.HasValue)
            version = await versions.SingleOrDefaultAsync(v => v.IsCurrent, ct);
        if (version is null)
        {
            if (allowUnclassifiedEmptyDraft && nodeIds.Count == 0 && !requestedVersion.HasValue)
                return new(null, null);
            return new(null, new CurriculumScopeConflictDto
            {
                Message = "Khối này chưa có khung tri thức đã duyệt hoặc phiên bản được chọn không phù hợp. Hãy chọn khung đã xuất bản trước khi thêm chủ đề/xuất bản giáo trình.",
                InvalidNodeIds = nodeIds.Select(Id).ToList()
            });
        }
        var allowed = await (from m in db.KnowledgeNodeGrades.AsNoTracking()
                             join n in db.KnowledgeNodes.AsNoTracking() on new { m.CenterId, m.NodeId } equals new { n.CenterId, n.NodeId }
                             where m.CenterId == center && m.GraphVersionId == version.GraphVersionId &&
                                 n.SubjectId == subject && n.IsActive && !n.IsDeleted
                             select m.NodeId).ToListAsync(ct);
        var allowedSet = allowed.ToHashSet();
        var invalid = nodeIds.Where(id => !allowedSet.Contains(id)).Select(Id).ToList();
        return invalid.Count == 0 ? new(version.GraphVersionId, null) : new(version.GraphVersionId,
            new CurriculumScopeConflictDto { InvalidNodeIds = invalid });
    }

    public static bool TryParseNodes(IEnumerable<string> values, out List<ulong> nodes)
    {
        nodes = new();
        var seen = new HashSet<ulong>();
        foreach (var value in values)
        {
            if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0 || !seen.Add(id))
                return false;
            nodes.Add(id);
        }
        return true;
    }

    private static string Id(ulong id) => id.ToString(CultureInfo.InvariantCulture);
}
