using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

internal sealed class CurriculumClassLink
{
    public Guid CurriculumId { get; set; }
    public Guid ClassId { get; set; }
}

internal static class CurriculumClassScopeQuery
{
    // Draft links are a plan. Published links come ONLY from the application ledger.
    // Restrict by class owner, including when this teacher uses a Shared curriculum.
    public static async Task<List<CurriculumClassLink>> ReadAsync(EduTwinDbContext db, Guid center, Guid actor, Guid? curriculumId, CancellationToken ct)
    {
        var planned = db.CurriculumClasses.AsNoTracking().Where(a => a.CenterId == center &&
            a.Curriculum != null && !a.Curriculum.IsDeleted && a.Curriculum.ReviewStatus == ReviewStatus.Draft && a.Curriculum.TeacherId == actor &&
            a.Class != null && !a.Class.IsDeleted && a.Class.TeacherId == actor)
            .Select(a => new CurriculumClassLink { CurriculumId = a.CurriculumId, ClassId = a.ClassId });
        var applied = db.ClassCurriculumApplications.AsNoTracking().Where(a => a.CenterId == center && a.EndedAt == null &&
            !a.Curriculum.IsDeleted && a.Curriculum.ReviewStatus == ReviewStatus.Published &&
            (a.Curriculum.TeacherId == actor || a.Curriculum.Visibility == MaterialVisibility.Shared) &&
            !a.Class.IsDeleted && a.Class.TeacherId == actor && a.Class.Status == ClassStatus.Active && a.Class.LearningScope == ClassLearningScope.Current)
            .Select(a => new CurriculumClassLink { CurriculumId = a.CurriculumId, ClassId = a.ClassId });
        if (curriculumId.HasValue)
        {
            planned = planned.Where(a => a.CurriculumId == curriculumId.Value);
            applied = applied.Where(a => a.CurriculumId == curriculumId.Value);
        }
        // Two bounded projections avoid provider UNION type-mapping differences in legacy GUID columns.
        var links = await planned.ToListAsync(ct);
        links.AddRange(await applied.ToListAsync(ct));
        return links.DistinctBy(a => (a.CurriculumId, a.ClassId)).OrderBy(a => a.ClassId).ToList();
    }
}
