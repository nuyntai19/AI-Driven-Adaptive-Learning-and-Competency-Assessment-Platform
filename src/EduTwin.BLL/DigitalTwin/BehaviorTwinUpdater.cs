using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public sealed class BehaviorTwinUpdater : IBehaviorTwinUpdater
{
    private readonly EduTwinDbContext _dbContext;

    public BehaviorTwinUpdater(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BehaviorTwin> UpdateAsync(
        Attempt attempt,
        Guid subjectId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        var twin = await _dbContext.BehaviorTwins
            .SingleOrDefaultAsync(
                b => b.CenterId == attempt.CenterId
                    && b.StudentId == attempt.StudentId
                    && b.SubjectId == subjectId
                    && !b.IsDeleted,
                cancellationToken);

        if (twin is null)
        {
            twin = new BehaviorTwin
            {
                CenterId = attempt.CenterId,
                StudentId = attempt.StudentId,
                SubjectId = subjectId,
                AvgTimeSpentSeconds = 0m,
                SkipRate = 0m,
                ChangeAnswerRate = 0m,
                AvgConfidence = 0m,
                ConfidenceCalibration = 0m,
                AttemptCount = 0,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
            _dbContext.BehaviorTwins.Add(twin);
        }

        var prevCount = twin.AttemptCount;
        var newCount = prevCount + 1u;
        var newCountDecimal = (decimal)newCount;

        // 1. Average Time Spent Seconds
        var totalTimeSpent = (twin.AvgTimeSpentSeconds * prevCount) + attempt.TimeSpentSeconds;
        twin.AvgTimeSpentSeconds = Math.Round(totalTimeSpent / newCountDecimal, 2, MidpointRounding.AwayFromZero);

        // 2. Skip Rate (percentage 0 - 100)
        var totalSkipped = (twin.SkipRate * prevCount / 100m) + (attempt.Skipped ? 1m : 0m);
        var skipRate = Math.Clamp((totalSkipped / newCountDecimal) * 100m, 0m, 100m);
        twin.SkipRate = Math.Round(skipRate, 2, MidpointRounding.AwayFromZero);

        // 3. Change Answer Rate (percentage 0 - 100)
        var totalChanges = (twin.ChangeAnswerRate * prevCount / 100m) + (attempt.AnswerChanges > 0 ? 1m : 0m);
        var changeRate = Math.Clamp((totalChanges / newCountDecimal) * 100m, 0m, 100m);
        twin.ChangeAnswerRate = Math.Round(changeRate, 2, MidpointRounding.AwayFromZero);

        // 4. Average Confidence (0 - 100)
        var totalConfidence = (twin.AvgConfidence * prevCount) + Math.Clamp(attempt.Confidence, 0m, 100m);
        var avgConfidence = Math.Clamp(totalConfidence / newCountDecimal, 0m, 100m);
        twin.AvgConfidence = Math.Round(avgConfidence, 2, MidpointRounding.AwayFromZero);

        // 5. Confidence Calibration (0 - 100)
        if (attempt.IsCorrect.HasValue)
        {
            var targetCorrectness = attempt.IsCorrect.Value ? 100m : 0m;
            var attemptCalibration = Math.Clamp(100m - Math.Abs(attempt.Confidence - targetCorrectness), 0m, 100m);
            var totalCalibration = (twin.ConfidenceCalibration * prevCount) + attemptCalibration;
            var avgCalibration = Math.Clamp(totalCalibration / newCountDecimal, 0m, 100m);
            twin.ConfidenceCalibration = Math.Round(avgCalibration, 2, MidpointRounding.AwayFromZero);
        }

        twin.AttemptCount = newCount;
        twin.UpdatedAt = utcNow;

        return twin;
    }
}
