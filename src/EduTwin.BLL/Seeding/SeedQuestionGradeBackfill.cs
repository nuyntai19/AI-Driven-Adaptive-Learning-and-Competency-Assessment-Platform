using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Seeding;

// Development seed upgrade only. Match the original template as well as its IDs;
// never classify a teacher-authored/edited question or overwrite an existing grade.
public sealed class SeedQuestionGradeBackfill(EduTwinDbContext db, TimeProvider clock)
{
    public async Task EnsureAsync(CancellationToken ct = default)
    {
        foreach (var centerA in new[] { true, false })
        {
            var seed = new EduTwinSeedFactory(centerA).CreateData();
            var centerId = seed.Center.CenterId;
            var first = seed.Questions.Min(q => q.QuestionId);
            var last = seed.Questions.Max(q => q.QuestionId);
            var candidates = await db.Questions.IgnoreQueryFilters()
                .Where(q => q.CenterId == centerId && q.QuestionId >= first && q.QuestionId <= last &&
                    !q.IsDeleted && q.GradeLevel == null).ToArrayAsync(ct);
            foreach (var question in candidates)
            {
                var template = seed.Questions.Single(q => q.QuestionId == question.QuestionId);
                if (question.SubjectId != template.SubjectId || question.PrimaryTopicNodeId != template.PrimaryTopicNodeId ||
                    question.CreatedByTeacherId != template.CreatedByTeacherId || question.QuestionText != template.QuestionText)
                    continue;
                question.GradeLevel = template.GradeLevel;
                question.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
