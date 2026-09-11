using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public sealed class KnowledgeTwinUpdater : IKnowledgeTwinUpdater
{
    private readonly EduTwinDbContext _dbContext;

    public KnowledgeTwinUpdater(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<KnowledgeTwinUpdateResult> UpdateAsync(
        Attempt attempt,
        Question question,
        ReasoningAnalysis analysis,
        EvidenceAssessment evidence,
        decimal confidenceCalibration,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(evidence);

        var twin = await _dbContext.KnowledgeTwins
            .SingleOrDefaultAsync(
                k => k.CenterId == attempt.CenterId
                    && k.StudentId == attempt.StudentId
                    && k.SubjectId == question.SubjectId
                    && k.TopicNodeId == question.PrimaryTopicNodeId
                    && !k.IsDeleted,
                cancellationToken);

        if (twin is null)
        {
            twin = new KnowledgeTwin
            {
                KnowledgeTwinId = TwinAggregateIdGenerator.NewId(),
                CenterId = attempt.CenterId,
                StudentId = attempt.StudentId,
                SubjectId = question.SubjectId,
                TopicNodeId = question.PrimaryTopicNodeId,
                MasteryPercentage = 0m,
                EvidenceCount = 0,
                LastReasoningQuality = null,
                LastAttemptId = null,
                LastEvidenceAt = null,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
            _dbContext.KnowledgeTwins.Add(twin);
        }

        var timeQuality = CalculateTimeQuality(attempt.TimeSpentSeconds, question.EstimatedTimeSeconds);
        var normalizedCalibration = Math.Clamp(confidenceCalibration / 100m, 0m, 1m);

        var input = new MasteryCalculationInput(
            CurrentMastery: twin.MasteryPercentage,
            ReasoningQuality: analysis.ReasoningQuality,
            ReasoningWeight: evidence.ReasoningWeight,
            IsCorrect: attempt.IsCorrect ?? false,
            TimeQuality: timeQuality,
            ConfidenceCalibration: evidence.ReasoningWeight > 0m && analysis.ReasoningQuality.HasValue ? normalizedCalibration : null,
            Difficulty: question.Difficulty);

        var calculation = MasteryCalculator.Calculate(input);

        if (evidence.ReasoningWeight > 0m)
        {
            twin.MasteryPercentage = calculation.NewMastery;
            twin.EvidenceCount += 1;
            twin.LastReasoningQuality = analysis.ReasoningQuality;
            twin.LastAttemptId = attempt.AttemptId;
            twin.LastEvidenceAt = utcNow;
        }

        twin.UpdatedAt = utcNow;

        return new KnowledgeTwinUpdateResult(twin, calculation);
    }

    private static decimal CalculateTimeQuality(uint timeSpentSeconds, uint estimatedTimeSeconds)
    {
        if (estimatedTimeSeconds == 0)
        {
            return 0.50m;
        }

        var ratio = timeSpentSeconds / (decimal)estimatedTimeSeconds;
        var quality = Math.Clamp(1m - (Math.Abs(ratio - 1m) * 0.5m), 0m, 1m);
        return Math.Round(quality, 2, MidpointRounding.AwayFromZero);
    }
}
