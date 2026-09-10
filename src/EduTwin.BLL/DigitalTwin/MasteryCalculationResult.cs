namespace EduTwin.BLL.DigitalTwin;

public sealed record MasteryCalculationBreakdown(
    bool IsFallback,
    decimal PreviousMastery,
    decimal? NormalizedReasoningQuality,
    decimal ReasoningWeight,
    decimal Correctness,
    decimal TimeQuality,
    decimal? ConfidenceCalibration,
    byte Difficulty,
    decimal DifficultyMultiplier,
    decimal LearningRate,
    decimal EvidenceTarget,
    decimal UnclampedNewMastery,
    decimal NewMastery,
    decimal Delta);

public sealed record MasteryCalculationResult(
    decimal PreviousMastery,
    decimal NewMastery,
    decimal Delta,
    decimal? EffectiveReasoningQuality,
    string CalculationVersion,
    MasteryCalculationBreakdown Breakdown,
    string Explanation);
