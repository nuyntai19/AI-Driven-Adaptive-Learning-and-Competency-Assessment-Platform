using System;
using System.Collections.Generic;
using System.Linq;

namespace EduTwin.BLL.DigitalTwin;

public sealed class BehaviorCalibrationCalculator : IBehaviorCalibrationCalculator
{
    public const decimal DefaultCalibration = 50.00m;

    public decimal CalculateCalibration(IEnumerable<GradedAttemptSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var list = samples as IReadOnlyList<GradedAttemptSample> ?? samples.ToList();
        if (list.Count == 0)
        {
            return DefaultCalibration;
        }

        var total = list.Sum(s => Math.Clamp(100m - Math.Abs(s.Confidence - (s.EffectiveIsCorrect ? 100m : 0m)), 0m, 100m));
        return Math.Round(total / list.Count, 2, MidpointRounding.AwayFromZero);
    }
}
