using System.Globalization;

namespace EduTwin.BLL.DigitalTwin;

public static class MasteryCalculator
{
    public const string CalculationVersion = "mastery-v1";

    public static MasteryCalculationResult Calculate(MasteryCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidatePercentage(input.CurrentMastery, nameof(input.CurrentMastery));

        if (input.ReasoningQuality is { } reasoningQuality)
        {
            ValidatePercentage(reasoningQuality, nameof(input.ReasoningQuality));
        }

        ValidateNormalizedFactor(input.ReasoningWeight, nameof(input.ReasoningWeight));

        ValidateNormalizedFactor(input.TimeQuality, nameof(input.TimeQuality));

        if (input.ReasoningWeight > 0m && input.ReasoningQuality is null)
        {
            throw new ArgumentNullException(
                nameof(input.ReasoningQuality),
                "Reasoning quality is required when evidence has a positive weight.");
        }

        if (input.ReasoningWeight > 0m && input.ConfidenceCalibration is null)
        {
            throw new ArgumentNullException(
                nameof(input.ConfidenceCalibration),
                "Confidence calibration is required for the Reasoning path.");
        }

        if (input.ConfidenceCalibration is { } confidenceCalibration)
        {
            ValidateNormalizedFactor(confidenceCalibration, nameof(input.ConfidenceCalibration));
        }

        if (input.Difficulty is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.Difficulty),
                input.Difficulty,
                "Difficulty must be between 1 and 5.");
        }

        var isFallback = input.ReasoningQuality is null;
        var normalizedReasoningQuality = input.ReasoningQuality / 100m;
        var correctness = input.IsCorrect ? 1m : 0m;
        var effectiveConfidenceCalibration = isFallback ? null : input.ConfidenceCalibration;
        var difficultyMultiplier = GetDifficultyMultiplier(input.Difficulty);
        var learningRate = input.ReasoningWeight == 0m ? 0m : 0.25m;

        var evidenceTarget = input.ReasoningWeight == 0m
            ? input.CurrentMastery
            : 100m * normalizedReasoningQuality!.Value *
                (0.65m +
                 0.20m * correctness +
                 0.10m * input.TimeQuality +
                 0.05m * effectiveConfidenceCalibration!.Value);

        var unclampedNewMastery = input.CurrentMastery +
            learningRate * difficultyMultiplier * input.ReasoningWeight *
            (evidenceTarget - input.CurrentMastery);
        var clampedNewMastery = Math.Clamp(unclampedNewMastery, 0m, 100m);
        var newMastery = RoundForPersistence(clampedNewMastery);
        var delta = RoundForPersistence(newMastery - input.CurrentMastery);

        var breakdown = new MasteryCalculationBreakdown(
            IsFallback: isFallback,
            PreviousMastery: input.CurrentMastery,
            NormalizedReasoningQuality: normalizedReasoningQuality,
            ReasoningWeight: input.ReasoningWeight,
            Correctness: correctness,
            TimeQuality: input.TimeQuality,
            ConfidenceCalibration: effectiveConfidenceCalibration,
            Difficulty: input.Difficulty,
            DifficultyMultiplier: difficultyMultiplier,
            LearningRate: learningRate,
            EvidenceTarget: evidenceTarget,
            UnclampedNewMastery: unclampedNewMastery,
            NewMastery: newMastery,
            Delta: delta);

        return new MasteryCalculationResult(
            PreviousMastery: input.CurrentMastery,
            NewMastery: newMastery,
            Delta: delta,
            EffectiveReasoningQuality: isFallback ? null : input.ReasoningQuality,
            CalculationVersion: CalculationVersion,
            Breakdown: breakdown,
            Explanation: BuildExplanation(
                isFallback,
                input.CurrentMastery,
                newMastery,
                delta,
                input.Difficulty,
                input.ReasoningQuality,
                input.ReasoningWeight));
    }

    private static void ValidatePercentage(decimal value, string paramName)
    {
        if (value < 0m || value > 100m)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be between 0 and 100.");
        }
    }

    private static void ValidateNormalizedFactor(decimal value, string paramName)
    {
        if (value < 0m || value > 1m)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be between 0 and 1.");
        }
    }

    private static decimal GetDifficultyMultiplier(byte difficulty) => difficulty switch
    {
        1 => 0.85m,
        2 => 0.925m,
        3 => 1.0m,
        4 => 1.075m,
        5 => 1.15m,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    private static decimal RoundForPersistence(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string BuildExplanation(
        bool isFallback,
        decimal previousMastery,
        decimal newMastery,
        decimal delta,
        byte difficulty,
        decimal? reasoningQuality,
        decimal reasoningWeight)
    {
        var path = isFallback ? "Fallback" : "Reasoning";
        var signedDelta = delta.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);
        var commonFacts = FormattableString.Invariant(
            $"{path} path: mastery {previousMastery:0.00} -> {newMastery:0.00} (delta {signedDelta}) at difficulty {difficulty}.");

        if (reasoningWeight == 0m)
        {
            return commonFacts + " Review-only evidence has weight 0.00 and cannot change mastery before teacher confirmation.";
        }

        return commonFacts + FormattableString.Invariant(
            $" Effective reasoning quality: {reasoningQuality!.Value:0.00}%; evidence weight: {reasoningWeight:0.00}.");
    }
}
