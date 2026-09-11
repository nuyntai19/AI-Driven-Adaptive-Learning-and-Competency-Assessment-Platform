using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.DigitalTwin;

public sealed class BehaviorCalibrationSampleProvider : IBehaviorCalibrationSampleProvider
{
    private readonly EduTwinDbContext _dbContext;

    public BehaviorCalibrationSampleProvider(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<SubjectCalibrationSample>> GetSubjectSamplesAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        Attempt currentAttempt,
        bool hasPendingCorrectnessOverride,
        bool? pendingCorrectnessOverride,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentAttempt);

        if (currentAttempt.CenterId != centerId || currentAttempt.StudentId != studentId)
        {
            throw new ArgumentException("Current attempt must belong to the requested center and student.", nameof(currentAttempt));
        }

        var persisted = await (
            from attempt in _dbContext.Attempts
            join question in _dbContext.Questions
                on new { attempt.CenterId, attempt.QuestionId }
                equals new { question.CenterId, question.QuestionId }
            join analysis in _dbContext.ReasoningAnalyses
                on new { attempt.CenterId, attempt.AttemptId }
                equals new { analysis.CenterId, analysis.AttemptId }
                into analyses
            from analysis in analyses.DefaultIfEmpty()
            where attempt.CenterId == centerId
                && attempt.StudentId == studentId
                && question.SubjectId == subjectId
            select new
            {
                attempt.AttemptId,
                attempt.CreatedAt,
                attempt.Confidence,
                PreliminaryIsCorrect = attempt.IsCorrect,
                OverrideIsCorrect = analysis == null ? null : analysis.OverrideIsCorrect
            })
            .ToListAsync(cancellationToken);

        var samples = persisted
            .Select(item =>
            {
                var effectiveIsCorrect = item.OverrideIsCorrect
                    ?? (item.AttemptId == currentAttempt.AttemptId
                        ? currentAttempt.IsCorrect
                        : item.PreliminaryIsCorrect);

                if (item.AttemptId == currentAttempt.AttemptId && hasPendingCorrectnessOverride)
                {
                    effectiveIsCorrect = pendingCorrectnessOverride;
                }

                return new SubjectCalibrationSample(
                    item.AttemptId,
                    item.CreatedAt,
                    item.Confidence,
                    effectiveIsCorrect);
            })
            .ToList();

        if (!samples.Any(item => item.AttemptId == currentAttempt.AttemptId))
        {
            samples.Add(new SubjectCalibrationSample(
                currentAttempt.AttemptId,
                currentAttempt.CreatedAt,
                currentAttempt.Confidence,
                hasPendingCorrectnessOverride
                    ? pendingCorrectnessOverride
                    : currentAttempt.IsCorrect));
        }

        return samples
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.AttemptId)
            .ToList();
    }
}
