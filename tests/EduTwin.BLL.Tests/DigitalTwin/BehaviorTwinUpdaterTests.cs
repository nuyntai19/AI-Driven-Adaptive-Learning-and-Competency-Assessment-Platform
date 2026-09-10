using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.DigitalTwin;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.DigitalTwin;

public sealed class BehaviorTwinUpdaterTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public BehaviorTwinUpdaterTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task UpdateAsync_FirstAttempt_InitializesBehaviorTwin()
    {
        var updater = new BehaviorTwinUpdater(_dbContext);
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        var attempt = new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 1,
            TimeSpentSeconds = 120,
            Confidence = 80m,
            AnswerChanges = 1,
            Skipped = false,
            IsCorrect = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await updater.UpdateAsync(attempt, _subjectId, now, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.NotNull(result);
        Assert.Equal(1u, result.AttemptCount);
        Assert.Equal(120m, result.AvgTimeSpentSeconds);
        Assert.Equal(0m, result.SkipRate);
        Assert.Equal(100m, result.ChangeAnswerRate);
        Assert.Equal(80m, result.AvgConfidence);
        // Correct (100) with confidence 80 => calibration = 100 - |80 - 100| = 80
        Assert.Equal(80m, result.ConfidenceCalibration);
    }

    [Fact]
    public async Task UpdateAsync_MultipleAttempts_ComputesAccurateCumulativeMetrics()
    {
        var updater = new BehaviorTwinUpdater(_dbContext);
        var now = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

        // Attempt 1: 60s, skipped false, changes 0, confidence 100, correct true => calib 100
        var attempt1 = new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 1,
            TimeSpentSeconds = 60,
            Confidence = 100m,
            AnswerChanges = 0,
            Skipped = false,
            IsCorrect = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await updater.UpdateAsync(attempt1, _subjectId, now, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Attempt 2: 120s, skipped true, changes 2, confidence 60, correct false => calib 100 - |60 - 0| = 40
        var attempt2 = new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 2,
            TimeSpentSeconds = 120,
            Confidence = 60m,
            AnswerChanges = 2,
            Skipped = true,
            IsCorrect = false,
            CreatedAt = now.AddMinutes(1),
            UpdatedAt = now.AddMinutes(1)
        };
        var result2 = await updater.UpdateAsync(attempt2, _subjectId, now.AddMinutes(1), CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        Assert.Equal(2u, result2.AttemptCount);
        // AvgTime: (60 + 120) / 2 = 90
        Assert.Equal(90m, result2.AvgTimeSpentSeconds);
        // SkipRate: 1 / 2 * 100 = 50%
        Assert.Equal(50m, result2.SkipRate);
        // ChangeRate: 1 / 2 * 100 = 50%
        Assert.Equal(50m, result2.ChangeAnswerRate);
        // AvgConfidence: (100 + 60) / 2 = 80%
        Assert.Equal(80m, result2.AvgConfidence);
        // Calibration: (100 + 40) / 2 = 70%
        Assert.Equal(70m, result2.ConfidenceCalibration);
    }
}
