using System.Collections.Generic;

namespace EduTwin.BLL.DigitalTwin;

public readonly record struct GradedAttemptSample(decimal Confidence, bool EffectiveIsCorrect);

public interface IBehaviorCalibrationCalculator
{
    decimal CalculateCalibration(IEnumerable<GradedAttemptSample> samples);
}
