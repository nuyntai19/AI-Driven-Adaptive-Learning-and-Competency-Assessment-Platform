using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Seeding;

// Development seed upgrade only: the seeded "Tiếng Anh" subject moved from ENGLISH to the standard
// code ENG (see SubjectCodeGenerator). Without this rename, databases seeded earlier no longer match
// the seed manifest and are reported as conflicting. Only the seed-owned subject (deterministic ID)
// that still carries the original code is touched, and never when ENG is already used in the center.
// Knowledge-node codes are not rewritten.
public sealed class SeedSubjectCodeBackfill(EduTwinDbContext db, TimeProvider clock)
{
    public const string LegacyEnglishCode = "ENGLISH";

    public async Task EnsureAsync(CancellationToken ct = default)
    {
        foreach (var centerA in new[] { true, false })
        {
            var seed = new EduTwinSeedFactory(centerA).CreateData();
            var centerId = seed.Center.CenterId;
            var englishId = centerA ? DeterministicSeedIds.CenterAEnglishSubjectId : DeterministicSeedIds.CenterBEnglishSubjectId;
            var target = seed.Subjects.Single(s => s.SubjectId == englishId).SubjectCode;

            var subject = await db.Subjects.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.CenterId == centerId && s.SubjectId == englishId && !s.IsDeleted && s.SubjectCode == LegacyEnglishCode, ct);
            if (subject is null) continue;
            var taken = await db.Subjects.IgnoreQueryFilters()
                .AnyAsync(s => s.CenterId == centerId && s.SubjectId != englishId && s.SubjectCode == target, ct);
            if (taken) continue;

            subject.SubjectCode = target;
            subject.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        }
        await db.SaveChangesAsync(ct);
    }
}
