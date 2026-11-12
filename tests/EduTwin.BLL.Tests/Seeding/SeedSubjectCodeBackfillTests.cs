using EduTwin.BLL.Seeding;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Seeding;

public sealed class SeedSubjectCodeBackfillTests
{
    private static EduTwinDbContext NewDb() =>
        new(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Subject LegacySeedSubject(bool centerA)
    {
        var subject = new EduTwinSeedFactory(centerA).CreateData().Subjects
            .Single(s => s.SubjectId == (centerA ? DeterministicSeedIds.CenterAEnglishSubjectId : DeterministicSeedIds.CenterBEnglishSubjectId));
        subject.SubjectCode = SeedSubjectCodeBackfill.LegacyEnglishCode;
        return subject;
    }

    [Fact]
    public void FreshSeed_UsesStandardSubjectCodes()
    {
        foreach (var centerA in new[] { true, false })
        {
            var codes = new EduTwinSeedFactory(centerA).CreateData().Subjects.Select(s => s.SubjectCode).OrderBy(c => c);
            Assert.Equal(["ENG", "MATH"], codes);
        }
    }

    [Fact]
    public async Task RenamesLegacySeedEnglishSubject_InBothCenters_AndIsIdempotent()
    {
        await using var db = NewDb();
        var a = LegacySeedSubject(true);
        var b = LegacySeedSubject(false);
        db.Subjects.AddRange(a, b);
        await db.SaveChangesAsync();

        var service = new SeedSubjectCodeBackfill(db, TimeProvider.System);
        await service.EnsureAsync();

        Assert.Equal("ENG", a.SubjectCode);
        Assert.Equal("ENG", b.SubjectCode);
        var updatedAt = a.UpdatedAt;
        await service.EnsureAsync();
        Assert.Equal(updatedAt, a.UpdatedAt);
    }

    [Fact]
    public async Task LeavesEditedForeignOrConflictingSubjectsAlone()
    {
        await using var db = NewDb();
        var edited = LegacySeedSubject(true);
        edited.SubjectCode = "ANHVAN"; // a center manager changed it: not ours to touch
        var conflicting = LegacySeedSubject(false);
        var seedB = new EduTwinSeedFactory(false).CreateData();
        var otherEng = new Subject
        {
            SubjectId = Guid.NewGuid(), CenterId = seedB.Center.CenterId, SubjectCode = "ENG", SubjectName = "English club",
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        var foreign = new Subject
        {
            SubjectId = Guid.NewGuid(), CenterId = Guid.NewGuid(), SubjectCode = "ENGLISH", SubjectName = "Tiếng Anh",
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Subjects.AddRange(edited, conflicting, otherEng, foreign);
        await db.SaveChangesAsync();

        await new SeedSubjectCodeBackfill(db, TimeProvider.System).EnsureAsync();

        Assert.Equal("ANHVAN", edited.SubjectCode);
        Assert.Equal("ENGLISH", conflicting.SubjectCode);
        Assert.Equal("ENGLISH", foreign.SubjectCode);
    }
}
