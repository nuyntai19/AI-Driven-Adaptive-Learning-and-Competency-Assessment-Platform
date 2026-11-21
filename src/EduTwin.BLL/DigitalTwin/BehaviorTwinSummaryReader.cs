using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.DigitalTwin;

// Read observed telemetry from attempts so submissions made before adaptive
// practice updated the behavior cache are included without replaying mastery.
internal static class BehaviorTwinSummaryReader
{
    public static async Task<BehaviorTwinSummaryDto> ReadAsync(EduTwinDbContext db,
        Guid centerId, Guid studentId, Guid subjectId, CancellationToken ct)
    {
        var observations = await db.Attempts.AsNoTracking()
            .Where(a => a.CenterId == centerId && a.StudentId == studentId &&
                a.Question.SubjectId == subjectId &&
                (a.Status == AttemptStatus.Completed || a.Status == AttemptStatus.NeedsTeacherReview))
            .Select(a => new { a.TimeSpentSeconds, a.Skipped, a.AnswerChanges, a.Confidence })
            .ToListAsync(ct);
        var calibration = await db.BehaviorTwins.AsNoTracking()
            .Where(b => b.CenterId == centerId && b.StudentId == studentId && b.SubjectId == subjectId && !b.IsDeleted)
            .Select(b => b.ConfidenceCalibration).FirstOrDefaultAsync(ct);
        var count = observations.Count;
        decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return new BehaviorTwinSummaryDto
        {
            AttemptCount = (uint)count,
            AvgTimeSpentSeconds = count == 0 ? 0 : Round(observations.Average(a => (decimal)a.TimeSpentSeconds)),
            SkipRate = count == 0 ? 0 : Round(100m * observations.Count(a => a.Skipped) / count),
            ChangeAnswerRate = count == 0 ? 0 : Round(100m * observations.Count(a => a.AnswerChanges > 0) / count),
            AvgConfidence = Round(observations.Where(a => a.Confidence.HasValue).Select(a => a.Confidence!.Value).DefaultIfEmpty().Average()),
            ConfidenceCalibration = calibration
        };
    }
}
