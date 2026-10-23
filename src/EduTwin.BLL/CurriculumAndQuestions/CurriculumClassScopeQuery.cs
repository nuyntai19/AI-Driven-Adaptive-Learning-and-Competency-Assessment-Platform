using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

internal sealed class CurriculumClassLink
{
    public Guid CurriculumId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string AcademicYear { get; set; } = string.Empty;
    public byte? GradeLevel { get; set; }
    public string? ApplicationRole { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsPlanned { get; set; }

    public CurriculumApplicationClassDto ToDto() => new()
    {
        ClassId = ClassId.ToString("D"), ClassName = ClassName, AcademicYear = AcademicYear,
        GradeLevel = GradeLevel, ApplicationRole = ApplicationRole, IsCurrent = IsCurrent, IsPlanned = IsPlanned
    };
}

internal static class CurriculumClassScopeQuery
{
    // Draft links are a plan. Published links come ONLY from the application ledger.
    // Restrict by class owner, including when this teacher uses a Shared curriculum.
    public static async Task<List<CurriculumClassLink>> ReadAsync(EduTwinDbContext db, Guid center, Guid actor, Guid? curriculumId, CancellationToken ct, bool history = false)
    {
        var planned = db.CurriculumClasses.AsNoTracking().Where(a => a.CenterId == center &&
            a.Curriculum != null && !a.Curriculum.IsDeleted && a.Curriculum.ReviewStatus == ReviewStatus.Draft && a.Curriculum.TeacherId == actor &&
            a.Class != null && !a.Class.IsDeleted && a.Class.TeacherId == actor &&
            a.Class.Status == ClassStatus.Active && a.Class.LearningScope == ClassLearningScope.Current && !history)
            .Select(a => new CurriculumClassLink { CurriculumId = a.CurriculumId, ClassId = a.ClassId,
                ClassName = a.Class!.ClassName, AcademicYear = a.Class.AcademicYear, GradeLevel = a.Class.GradeLevel,
                IsPlanned = true, IsCurrent = true });
        var applications = db.ClassCurriculumApplications.AsNoTracking().Where(a => a.CenterId == center &&
            !a.Curriculum.IsDeleted && (a.Curriculum.ReviewStatus == ReviewStatus.Published || (history && a.Curriculum.ReviewStatus == ReviewStatus.Archived)) &&
            (a.Curriculum.TeacherId == actor || a.Curriculum.Visibility == MaterialVisibility.Shared) &&
            !a.Class.IsDeleted && a.Class.TeacherId == actor);
        applications = history
            ? applications.Where(a => a.EndedAt != null || a.Class.Status == ClassStatus.Archived || a.Class.LearningScope == ClassLearningScope.History)
            : applications.Where(a => a.EndedAt == null && a.Class.Status == ClassStatus.Active && a.Class.LearningScope == ClassLearningScope.Current);
        var applied = applications.Select(a => new CurriculumClassLink { CurriculumId = a.CurriculumId, ClassId = a.ClassId,
            ClassName = a.Class.ClassName, AcademicYear = a.Class.AcademicYear, GradeLevel = a.Class.GradeLevel,
            ApplicationRole = a.ApplicationRole, IsCurrent = a.EndedAt == null && a.Class.Status == ClassStatus.Active && a.Class.LearningScope == ClassLearningScope.Current,
            IsPlanned = false });
        if (curriculumId.HasValue)
        {
            planned = planned.Where(a => a.CurriculumId == curriculumId.Value);
            applied = applied.Where(a => a.CurriculumId == curriculumId.Value);
        }
        // Two bounded projections avoid provider UNION type-mapping differences in legacy GUID columns.
        var links = await planned.ToListAsync(ct);
        links.AddRange(await applied.ToListAsync(ct));
        return links.DistinctBy(a => (a.CurriculumId, a.ClassId, a.ApplicationRole, a.IsCurrent, a.IsPlanned)).OrderBy(a => a.ClassId).ToList();
    }
}
