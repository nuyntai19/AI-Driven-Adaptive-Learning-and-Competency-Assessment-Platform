using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.DigitalTwin;

public readonly record struct SubjectCalibrationSample(
    ulong AttemptId,
    DateTime CreatedAt,
    decimal Confidence,
    bool? EffectiveIsCorrect);

public interface IBehaviorCalibrationSampleProvider
{
    Task<IReadOnlyList<SubjectCalibrationSample>> GetSubjectSamplesAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        Attempt currentAttempt,
        bool hasPendingCorrectnessOverride,
        bool? pendingCorrectnessOverride,
        CancellationToken cancellationToken);
}
