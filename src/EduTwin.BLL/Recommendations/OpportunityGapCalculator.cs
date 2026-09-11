using System;
using System.Collections.Generic;
using System.Linq;

namespace EduTwin.BLL.Recommendations;

public sealed record TopicCandidateEvaluationInput(
    ulong TopicNodeId,
    string TopicName,
    uint OrderIndex,
    decimal ExamImportance,
    uint EstimatedLearningMinutes,
    decimal CurrentMastery,
    IReadOnlyList<decimal> PrerequisiteMasteries,
    decimal? WeightedRecentReasoningAverage,
    int ReasoningQualitySampleCount = 0,
    decimal ReasoningWeightSum = 0m,
    string ReasoningQualitySource = "SubjectFallback");

public sealed record OpportunityCandidateScored(
    ulong TopicNodeId,
    string TopicName,
    uint OrderIndex,
    decimal ExamImportance,
    uint EstimatedLearningMinutes,
    decimal EstimatedLearningHours,
    decimal CurrentMastery,
    decimal PrerequisiteReadiness,
    decimal RecentReasoningAverage01,
    decimal ProbabilityOfMastery,
    decimal ExpectedScoreGain,
    decimal RawOpportunity,
    decimal NormalizedOpportunityScore,
    int Rank,
    int ReasoningQualitySampleCount = 0,
    decimal ReasoningWeightSum = 0m,
    string ReasoningQualitySource = "SubjectFallback");

public static class OpportunityGapCalculator
{
    public const string Version = "opportunity-v1";

    public static IReadOnlyList<OpportunityCandidateScored> Evaluate(
        IReadOnlyList<TopicCandidateEvaluationInput> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return Array.Empty<OpportunityCandidateScored>();
        }

        var unranked = candidates.Select(c =>
        {
            var mastery01 = Math.Clamp(c.CurrentMastery / 100m, 0m, 1m);
            var reasoning01 = Math.Clamp((c.WeightedRecentReasoningAverage ?? 50m) / 100m, 0m, 1m);
            var prerequisiteReadiness = c.PrerequisiteMasteries.Count > 0
                ? Math.Clamp(c.PrerequisiteMasteries.Average() / 100m, 0m, 1m)
                : 1.0m;

            var estimatedHours = c.EstimatedLearningMinutes / 60.0m;
            var learningEffort = Math.Max(estimatedHours, 0.5m);

            var expectedScoreGain = (1m - mastery01) * c.ExamImportance;
            var probabilityOfMastery = Math.Clamp(
                0.20m + (0.60m * reasoning01) + (0.20m * prerequisiteReadiness),
                0m,
                1m);

            var rawOpportunity = (expectedScoreGain * probabilityOfMastery) / learningEffort;

            return (Candidate: c, EstimatedHours: estimatedHours, Readiness: prerequisiteReadiness,
                    Reasoning01: reasoning01, Prob: probabilityOfMastery, Gain: expectedScoreGain, Raw: rawOpportunity);
        }).ToList();

        decimal minRaw = unranked.Min(u => u.Raw);
        decimal maxRaw = unranked.Max(u => u.Raw);
        bool allEqual = (maxRaw - minRaw) == 0m || unranked.Count == 1;

        var normalizedList = unranked.Select(u =>
        {
            decimal normalizedScore = allEqual
                ? 100.00m
                : Math.Round(100m * (u.Raw - minRaw) / (maxRaw - minRaw), 2, MidpointRounding.AwayFromZero);

            return new
            {
                u.Candidate,
                u.EstimatedHours,
                u.Readiness,
                u.Reasoning01,
                u.Prob,
                u.Gain,
                u.Raw,
                NormalizedScore = normalizedScore
            };
        }).ToList();

        // Tie-break: Normalized DESC -> Mastery ASC -> ExamImportance DESC -> OrderIndex ASC -> TopicNodeId ASC
        var ordered = normalizedList
            .OrderByDescending(n => n.NormalizedScore)
            .ThenBy(n => n.Candidate.CurrentMastery)
            .ThenByDescending(n => n.Candidate.ExamImportance)
            .ThenBy(n => n.Candidate.OrderIndex)
            .ThenBy(n => n.Candidate.TopicNodeId)
            .ToList();

        return ordered.Select((item, index) => new OpportunityCandidateScored(
            TopicNodeId: item.Candidate.TopicNodeId,
            TopicName: item.Candidate.TopicName,
            OrderIndex: item.Candidate.OrderIndex,
            ExamImportance: item.Candidate.ExamImportance,
            EstimatedLearningMinutes: item.Candidate.EstimatedLearningMinutes,
            EstimatedLearningHours: item.EstimatedHours,
            CurrentMastery: item.Candidate.CurrentMastery,
            PrerequisiteReadiness: item.Readiness,
            RecentReasoningAverage01: item.Reasoning01,
            ProbabilityOfMastery: item.Prob,
            ExpectedScoreGain: item.Gain,
            RawOpportunity: item.Raw,
            NormalizedOpportunityScore: item.NormalizedScore,
            Rank: index + 1,
            ReasoningQualitySampleCount: item.Candidate.ReasoningQualitySampleCount,
            ReasoningWeightSum: item.Candidate.ReasoningWeightSum,
            ReasoningQualitySource: item.Candidate.ReasoningQualitySource
        )).ToList();
    }
}
