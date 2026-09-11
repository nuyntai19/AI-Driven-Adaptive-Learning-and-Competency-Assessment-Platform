using System;

namespace EduTwin.Contracts.Recommendations;

public sealed record OpportunityGapBreakdown(
    string Strategy,
    string CalculationVersion,
    int EffectiveEvidenceCount,
    decimal MasteryPercentage,
    decimal ExamImportance,
    uint EstimatedLearningMinutes,
    decimal EstimatedLearningHours,
    decimal? WeightedRecentReasoningAverage,
    int ReasoningQualitySampleCount,
    decimal ReasoningWeightSum,
    string ReasoningQualitySource,
    decimal PrerequisiteReadiness,
    decimal ProbabilityOfMastery,
    decimal ExpectedScoreGain,
    decimal RawOpportunity,
    decimal? NormalizedOpportunityScore,
    int CandidateCount,
    int TieBreakRank,
    string TieBreakFactors);
