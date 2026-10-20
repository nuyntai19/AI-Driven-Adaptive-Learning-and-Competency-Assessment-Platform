using EduTwin.BLL.Seeding;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Seeding;

public sealed class SeedQuestionGradeBackfillTests
{
    [Fact]
    public async Task Backfill_OnlyOriginalUnclassifiedSeedQuestions_AndIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new EduTwinDbContext(options);
        var seed = new EduTwinSeedFactory(true).CreateData();
        var missing = seed.Questions[0]; missing.GradeLevel = null;
        var edited = seed.Questions[1]; edited.GradeLevel = null; edited.QuestionText = "Teacher edited question";
        var classified = seed.Questions[2]; classified.GradeLevel = 11;
        var foreign = new EduTwinSeedFactory(false).CreateData().Questions[0];
        foreign.CenterId = Guid.NewGuid(); foreign.GradeLevel = null;
        db.Questions.AddRange(missing, edited, classified, foreign);
        await db.SaveChangesAsync();
        var service = new SeedQuestionGradeBackfill(db, TimeProvider.System);
        await service.EnsureAsync();
        Assert.Equal((byte)10, missing.GradeLevel);
        Assert.Null(edited.GradeLevel);
        Assert.Equal((byte)11, classified.GradeLevel);
        Assert.Null(foreign.GradeLevel);
        var version = missing.RowVersion;
        await service.EnsureAsync();
        Assert.Equal(version, missing.RowVersion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FreshSeed_HasGradeForEveryQuestion_AndRespectsAdvancedMath(bool centerA)
    {
        var data = new EduTwinSeedFactory(centerA).CreateData();
        Assert.All(data.Questions, q => Assert.InRange(q.GradeLevel!.Value, (byte)10, (byte)12));
        Assert.Equal((byte)12, data.Questions.Single(q => q.QuestionText.Contains("nghịch biến trên khoảng")).GradeLevel);
        foreach (var cls in data.Classes.Where(c => c.GradeLevel == 10))
            Assert.Contains(data.Questions, q => q.SubjectId == cls.SubjectId && q.GradeLevel == cls.GradeLevel);
    }
}
