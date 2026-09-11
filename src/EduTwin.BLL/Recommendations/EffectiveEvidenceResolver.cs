using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.Recommendations;

public static class EffectiveEvidenceResolver
{
    public static bool? GetEffectiveCorrectness(Attempt attempt, ReasoningAnalysis? analysis) =>
        analysis?.OverrideIsCorrect ?? attempt.IsCorrect;

    public static IReadOnlyList<EvidenceAssessment> GetEffectiveHeads(IEnumerable<EvidenceAssessment> assessments)
    {
        ArgumentNullException.ThrowIfNull(assessments);

        var list = assessments.ToList();
        if (list.Count == 0)
        {
            return Array.Empty<EvidenceAssessment>();
        }

        // An assessment is a head if it has not been superseded by any other assessment
        var supersededIds = new HashSet<ulong>(
            list.Where(a => a.SupersedesAssessmentId.HasValue)
                .Select(a => a.SupersedesAssessmentId!.Value));

        var heads = list.Where(a => !supersededIds.Contains(a.EvidenceAssessmentId)).ToList();
        var duplicateAttempt = heads
            .GroupBy(h => h.AttemptId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateAttempt != null)
        {
            throw new InvalidOperationException($"Invariant violation: Multiple unsuperseded evidence assessment heads found for AttemptId {duplicateAttempt.Key}.");
        }

        return heads;
    }

    public static int CountEffectiveGovernedEvidence(IEnumerable<EvidenceAssessment> assessments)
    {
        var heads = GetEffectiveHeads(assessments);
        return heads.Count(h =>
            h.ReasoningWeight > 0m
            && GetEffectiveCorrectness(h.Attempt, h.Analysis) != null);
    }

    public static (decimal? WeightedAverage, int SampleCount, decimal WeightSum) CalculateWeightedReasoningAverage(
        IEnumerable<EvidenceAssessment> heads)
    {
        ArgumentNullException.ThrowIfNull(heads);

        decimal sumWeightedQuality = 0m;
        decimal sumWeight = 0m;
        int sampleCount = 0;

        foreach (var head in heads)
        {
            if (head.ReasoningWeight <= 0m)
            {
                continue;
            }

            decimal? quality = head.Analysis != null
                ? (head.Analysis.OverrideReasoningQuality ?? head.Analysis.ReasoningQuality)
                : null;

            if (quality.HasValue)
            {
                sumWeightedQuality += quality.Value * head.ReasoningWeight;
                sumWeight += head.ReasoningWeight;
                sampleCount++;
            }
        }

        if (sampleCount == 0 || sumWeight == 0m)
        {
            return (null, 0, 0m);
        }

        var average = Math.Round(sumWeightedQuality / sumWeight, 2, MidpointRounding.AwayFromZero);
        return (average, sampleCount, sumWeight);
    }
}
