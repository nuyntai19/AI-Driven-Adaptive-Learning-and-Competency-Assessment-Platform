using System;

namespace EduTwin.BLL.DigitalTwin;

public static class StudentSubjectGoalRiskCalculator
{
    public static decimal CalculateRisk(decimal targetScore, decimal predictedScore, int remainingDays)
    {
        if (targetScore < 0m || targetScore > 10m)
            throw new ArgumentOutOfRangeException(nameof(targetScore), "Target score must be between 0 and 10.");

        if (predictedScore < 0m || predictedScore > 10m)
            throw new ArgumentOutOfRangeException(nameof(predictedScore), "Predicted score must be between 0 and 10.");

        if (remainingDays < 0 || remainingDays > 3650)
            throw new ArgumentOutOfRangeException(nameof(remainingDays), "Remaining days must be between 0 and 3650.");

        var gap = targetScore - predictedScore;
        var rawScoreGap = gap / 10m;
        var scoreGap = Math.Max(0m, Math.Min(rawScoreGap, 1m));

        var rawTimePressure = remainingDays / 180m;
        var clampedTimePressure = Math.Max(0m, Math.Min(rawTimePressure, 1m));
        var timePressure = 1m - clampedTimePressure;

        var rawRiskScore = 100m * scoreGap * (0.70m + 0.30m * timePressure);

        // Round to exactly 2 decimal places using deterministic strategy (MidpointRounding.AwayFromZero)
        return Math.Round(rawRiskScore, 2, MidpointRounding.AwayFromZero);
    }
}
