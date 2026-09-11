namespace EduTwin.BLL.DigitalTwin;

public sealed record ReplayStepBreakdown(
    ulong AttemptId,
    decimal? EffectiveReasoningQuality,
    decimal ReasoningWeight,
    bool? EffectiveCorrectness,
    decimal TimeQuality,
    decimal RollingCalibration,
    byte Difficulty,
    decimal DifficultyMultiplier,
    decimal LearningRate,
    decimal PreviousMastery,
    decimal NewMastery,
    decimal Delta);

public sealed record ReplaySummaryBreakdown(
    ulong TriggerAttemptId,
    decimal PreviousMastery,
    decimal FinalMastery,
    int ReplayCount,
    int EffectiveEvidenceCount,
    decimal FinalCalibration,
    IReadOnlyList<ReplayStepBreakdown> ReplaySteps);

public sealed record MasteryCalculationBreakdown(
    bool IsFallback,
    decimal PreviousMastery,
    decimal? NormalizedReasoningQuality,
    decimal ReasoningWeight,
    decimal? Correctness,
    decimal TimeQuality,
    decimal? ConfidenceCalibration,
    byte Difficulty,
    decimal DifficultyMultiplier,
    decimal LearningRate,
    decimal EvidenceTarget,
    decimal UnclampedNewMastery,
    decimal NewMastery,
    decimal Delta,
    IReadOnlyList<ReplayStepBreakdown>? ReplaySteps = null);

public sealed record MasteryCalculationResult(
    decimal PreviousMastery,
    decimal NewMastery,
    decimal Delta,
    decimal? EffectiveReasoningQuality,
    string CalculationVersion,
    MasteryCalculationBreakdown Breakdown,
    string Explanation,
    object? HistoryBreakdown = null);
