using System.Globalization;
using EduTwin.BLL.DigitalTwin;
using Xunit;

namespace EduTwin.BLL.Tests.DigitalTwin;

public class MasteryCalculatorTests
{
    public static TheoryData<MasteryCalculationInput, decimal> NumericAcceptanceScenarios => new()
    {
        { Input(currentMastery: 0m, reasoningQuality: 20m), 5.00m },
        { Input(currentMastery: 50m, reasoningQuality: 80m), 57.50m },
        {
            Input(
                currentMastery: 0m,
                reasoningQuality: null,
                confidenceCalibration: null),
            2.50m
        },
        {
            Input(
                currentMastery: 40m,
                reasoningQuality: null,
                isCorrect: false,
                timeQuality: 0m,
                confidenceCalibration: null),
            36.00m
        }
    };

    public static TheoryData<decimal, decimal> ReasoningQualityBoundaries => new()
    {
        { 0m, 0.00m },
        { 39m, 9.75m },
        { 40m, 10.00m },
        { 59m, 14.75m },
        { 60m, 15.00m },
        { 79m, 19.75m },
        { 80m, 20.00m },
        { 100m, 25.00m }
    };

    public static TheoryData<byte, decimal, decimal, decimal> DifficultyCases => new()
    {
        { 1, 0.85m, 21.25m, 21.25m },
        { 2, 0.925m, 23.125m, 23.13m },
        { 3, 1.0m, 25.00m, 25.00m },
        { 4, 1.075m, 26.875m, 26.88m },
        { 5, 1.15m, 28.75m, 28.75m }
    };

    public static TheoryData<byte, decimal, decimal> MidpointRoundingCases => new()
    {
        { 2, 4.625m, 4.63m },
        { 4, 5.375m, 5.38m }
    };

    public static TheoryData<decimal> InvalidPercentages => new()
    {
        -0.01m,
        100.01m
    };

    public static TheoryData<decimal> InvalidNormalizedFactors => new()
    {
        -0.01m,
        1.01m
    };

    [Theory]
    [MemberData(nameof(NumericAcceptanceScenarios))]
    public void Calculate_NumericAcceptanceScenario_ReturnsExpectedMastery(
        MasteryCalculationInput input,
        decimal expectedMastery)
    {
        var result = MasteryCalculator.Calculate(input);

        Assert.Equal(expectedMastery, result.NewMastery);
    }

    [Theory]
    [MemberData(nameof(ReasoningQualityBoundaries))]
    public void Calculate_ReasoningQualityBoundary_ReturnsExpectedMastery(
        decimal reasoningQuality,
        decimal expectedMastery)
    {
        var result = MasteryCalculator.Calculate(Input(reasoningQuality: reasoningQuality));

        Assert.False(result.Breakdown.IsFallback);
        Assert.Equal(expectedMastery, result.NewMastery);
    }

    [Fact]
    public void Calculate_NullReasoningQuality_UsesFallbackInsteadOfZeroReasoningPath()
    {
        var fallback = MasteryCalculator.Calculate(Input(reasoningQuality: null));
        var zeroReasoning = MasteryCalculator.Calculate(Input(reasoningQuality: 0m));

        Assert.True(fallback.Breakdown.IsFallback);
        Assert.Equal(0.10m, fallback.Breakdown.LearningRate);
        Assert.False(zeroReasoning.Breakdown.IsFallback);
        Assert.Equal(0.25m, zeroReasoning.Breakdown.LearningRate);
        Assert.Equal(2.50m, fallback.NewMastery);
        Assert.Equal(0.00m, zeroReasoning.NewMastery);
    }

    [Theory]
    [MemberData(nameof(DifficultyCases))]
    public void Calculate_DifficultyMapping_UsesExactMultiplierAndFormula(
        byte difficulty,
        decimal expectedMultiplier,
        decimal expectedUnclampedMastery,
        decimal expectedMastery)
    {
        var result = MasteryCalculator.Calculate(Input(reasoningQuality: 100m, difficulty: difficulty));

        Assert.Equal(expectedMultiplier, result.Breakdown.DifficultyMultiplier);
        Assert.Equal(expectedUnclampedMastery, result.Breakdown.UnclampedNewMastery);
        Assert.Equal(expectedMastery, result.NewMastery);
    }

    [Theory]
    [MemberData(nameof(MidpointRoundingCases))]
    public void Calculate_MidpointValue_RoundsAwayFromZeroAtResultBoundary(
        byte difficulty,
        decimal expectedUnclampedMastery,
        decimal expectedMastery)
    {
        var result = MasteryCalculator.Calculate(Input(reasoningQuality: 20m, difficulty: difficulty));

        Assert.Equal(expectedUnclampedMastery, result.Breakdown.UnclampedNewMastery);
        Assert.Equal(expectedMastery, result.NewMastery);
        Assert.Equal(expectedMastery, result.Delta);
    }

    [Fact]
    public void Calculate_ReasoningPath_PopulatesCompleteBreakdown()
    {
        var input = Input(
            currentMastery: 50m,
            reasoningQuality: 80m,
            isCorrect: false,
            timeQuality: 0.5m,
            confidenceCalibration: 0.25m,
            difficulty: 4);

        var result = MasteryCalculator.Calculate(input);

        Assert.Equal(50m, result.PreviousMastery);
        Assert.Equal(51.88m, result.NewMastery);
        Assert.Equal(1.88m, result.Delta);
        Assert.Equal(80m, result.EffectiveReasoningQuality);
        Assert.False(result.Breakdown.IsFallback);
        Assert.Equal(50m, result.Breakdown.PreviousMastery);
        Assert.Equal(0.8m, result.Breakdown.NormalizedReasoningQuality);
        Assert.Equal(0m, result.Breakdown.Correctness);
        Assert.Equal(0.5m, result.Breakdown.TimeQuality);
        Assert.Equal(0.25m, result.Breakdown.ConfidenceCalibration);
        Assert.Equal((byte)4, result.Breakdown.Difficulty);
        Assert.Equal(1.075m, result.Breakdown.DifficultyMultiplier);
        Assert.Equal(0.25m, result.Breakdown.LearningRate);
        Assert.Equal(57m, result.Breakdown.EvidenceTarget);
        Assert.Equal(51.88125m, result.Breakdown.UnclampedNewMastery);
        Assert.Equal(result.NewMastery, result.Breakdown.NewMastery);
        Assert.Equal(result.Delta, result.Breakdown.Delta);
    }

    [Fact]
    public void Calculate_FallbackPath_PopulatesCompleteBreakdownAndIgnoresValidConfidence()
    {
        var withoutConfidence = MasteryCalculator.Calculate(Input(
            currentMastery: 40m,
            reasoningQuality: null,
            isCorrect: false,
            timeQuality: 0.5m,
            confidenceCalibration: null,
            difficulty: 2));
        var withConfidence = MasteryCalculator.Calculate(Input(
            currentMastery: 40m,
            reasoningQuality: null,
            isCorrect: false,
            timeQuality: 0.5m,
            confidenceCalibration: 0.75m,
            difficulty: 2));

        Assert.Equal(withoutConfidence, withConfidence);
        Assert.Null(withoutConfidence.EffectiveReasoningQuality);
        Assert.True(withoutConfidence.Breakdown.IsFallback);
        Assert.Null(withoutConfidence.Breakdown.NormalizedReasoningQuality);
        Assert.Equal(0m, withoutConfidence.Breakdown.Correctness);
        Assert.Equal(0.5m, withoutConfidence.Breakdown.TimeQuality);
        Assert.Null(withoutConfidence.Breakdown.ConfidenceCalibration);
        Assert.Equal(0.10m, withoutConfidence.Breakdown.LearningRate);
        Assert.Equal(2.5m, withoutConfidence.Breakdown.EvidenceTarget);
        Assert.Equal(36.53125m, withoutConfidence.Breakdown.UnclampedNewMastery);
        Assert.Equal(36.53m, withoutConfidence.NewMastery);
        Assert.Equal(-3.47m, withoutConfidence.Delta);
    }

    [Fact]
    public void Calculate_ReasoningPath_UsesEveryNormalizedFactor()
    {
        var allEvidence = MasteryCalculator.Calculate(Input(reasoningQuality: 100m));
        var lowerReasoning = MasteryCalculator.Calculate(Input(reasoningQuality: 80m));
        var incorrect = MasteryCalculator.Calculate(Input(reasoningQuality: 100m, isCorrect: false));
        var poorTime = MasteryCalculator.Calculate(Input(reasoningQuality: 100m, timeQuality: 0m));
        var poorCalibration = MasteryCalculator.Calculate(Input(
            reasoningQuality: 100m,
            confidenceCalibration: 0m));

        Assert.Equal(25.00m, allEvidence.NewMastery);
        Assert.Equal(20.00m, lowerReasoning.NewMastery);
        Assert.Equal(20.00m, incorrect.NewMastery);
        Assert.Equal(22.50m, poorTime.NewMastery);
        Assert.Equal(23.75m, poorCalibration.NewMastery);
    }

    [Fact]
    public void Calculate_FallbackPath_UsesOnlyCorrectnessAndTimeQuality()
    {
        var allEvidence = MasteryCalculator.Calculate(Input(reasoningQuality: null));
        var incorrect = MasteryCalculator.Calculate(Input(reasoningQuality: null, isCorrect: false));
        var poorTime = MasteryCalculator.Calculate(Input(reasoningQuality: null, timeQuality: 0m));

        Assert.Equal(2.50m, allEvidence.NewMastery);
        Assert.Equal(0.50m, incorrect.NewMastery);
        Assert.Equal(2.00m, poorTime.NewMastery);
    }

    [Fact]
    public void Calculate_IncorrectWithStrongReasoning_RecordsPartialMastery()
    {
        var result = MasteryCalculator.Calculate(Input(reasoningQuality: 80m, isCorrect: false));

        Assert.Equal(16.00m, result.NewMastery);
        Assert.True(result.NewMastery > 0m);
    }

    [Fact]
    public void Calculate_SameCorrectEvidence_FallbackIncreaseIsLowerThanReasoningIncrease()
    {
        var reasoning = MasteryCalculator.Calculate(Input(reasoningQuality: 80m));
        var fallback = MasteryCalculator.Calculate(Input(reasoningQuality: null));

        Assert.Equal(20.00m, reasoning.NewMastery);
        Assert.Equal(2.50m, fallback.NewMastery);
        Assert.True(fallback.Delta < reasoning.Delta);
    }

    [Fact]
    public void Calculate_StrongEvidenceAboveCurrentMastery_IncreasesMastery()
    {
        var result = MasteryCalculator.Calculate(Input(currentMastery: 50m, reasoningQuality: 100m));

        Assert.Equal(62.50m, result.NewMastery);
        Assert.Equal(12.50m, result.Delta);
    }

    [Fact]
    public void Calculate_WeakEvidenceBelowCurrentMastery_DecreasesMastery()
    {
        var result = MasteryCalculator.Calculate(Input(currentMastery: 80m, reasoningQuality: 0m));

        Assert.Equal(60.00m, result.NewMastery);
        Assert.Equal(-20.00m, result.Delta);
    }

    [Fact]
    public void Calculate_ZeroBoundary_RemainsWithinClamp()
    {
        var result = MasteryCalculator.Calculate(Input(
            currentMastery: 0m,
            reasoningQuality: 0m,
            isCorrect: false,
            timeQuality: 0m,
            confidenceCalibration: 0m));

        Assert.Equal(0.00m, result.NewMastery);
        Assert.Equal(0.00m, result.Breakdown.NewMastery);
        Assert.InRange(result.NewMastery, 0m, 100m);
    }

    [Fact]
    public void Calculate_HundredBoundary_RemainsWithinClamp()
    {
        var result = MasteryCalculator.Calculate(Input(currentMastery: 100m, reasoningQuality: 100m));

        Assert.Equal(100.00m, result.NewMastery);
        Assert.Equal(100.00m, result.Breakdown.NewMastery);
        Assert.InRange(result.NewMastery, 0m, 100m);
    }

    [Fact]
    public void Calculate_NullInput_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => MasteryCalculator.Calculate(null!));

        Assert.Equal("input", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidPercentages))]
    public void Calculate_InvalidCurrentMastery_ThrowsForCurrentMastery(decimal currentMastery)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(currentMastery: currentMastery)));

        Assert.Equal("CurrentMastery", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidPercentages))]
    public void Calculate_InvalidReasoningQuality_ThrowsForReasoningQuality(decimal reasoningQuality)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(reasoningQuality: reasoningQuality)));

        Assert.Equal("ReasoningQuality", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidNormalizedFactors))]
    public void Calculate_InvalidTimeQuality_ThrowsForTimeQuality(decimal timeQuality)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(timeQuality: timeQuality)));

        Assert.Equal("TimeQuality", exception.ParamName);
    }

    [Fact]
    public void Calculate_ReasoningPathWithoutConfidence_ThrowsForConfidenceCalibration()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            MasteryCalculator.Calculate(Input(confidenceCalibration: null)));

        Assert.Equal("ConfidenceCalibration", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidNormalizedFactors))]
    public void Calculate_InvalidReasoningConfidence_ThrowsForConfidenceCalibration(decimal confidenceCalibration)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(confidenceCalibration: confidenceCalibration)));

        Assert.Equal("ConfidenceCalibration", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidNormalizedFactors))]
    public void Calculate_InvalidFallbackConfidence_IsRejectedEvenThoughUnused(decimal confidenceCalibration)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(
                reasoningQuality: null,
                confidenceCalibration: confidenceCalibration)));

        Assert.Equal("ConfidenceCalibration", exception.ParamName);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)6)]
    public void Calculate_InvalidDifficulty_ThrowsForDifficulty(byte difficulty)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(difficulty: difficulty)));

        Assert.Equal("Difficulty", exception.ParamName);
    }

    [Fact]
    public void Calculate_ValidRangeBoundaries_AreAccepted()
    {
        var minimum = MasteryCalculator.Calculate(Input(
            currentMastery: 0m,
            reasoningQuality: 0m,
            timeQuality: 0m,
            confidenceCalibration: 0m,
            difficulty: 1));
        var maximum = MasteryCalculator.Calculate(Input(
            currentMastery: 100m,
            reasoningQuality: 100m,
            timeQuality: 1m,
            confidenceCalibration: 1m,
            difficulty: 5));

        Assert.InRange(minimum.NewMastery, 0m, 100m);
        Assert.InRange(maximum.NewMastery, 0m, 100m);
    }

    [Fact]
    public void Calculate_MultipleInvalidValues_FailsFastInSpecifiedOrder()
    {
        var currentMasteryFirst = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(
                currentMastery: -0.01m,
                reasoningQuality: 100.01m,
                timeQuality: 1.01m,
                confidenceCalibration: null,
                difficulty: 0)));
        var reasoningFirst = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(
                reasoningQuality: 100.01m,
                timeQuality: 1.01m,
                confidenceCalibration: null,
                difficulty: 0)));
        var timeFirst = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(
                timeQuality: 1.01m,
                confidenceCalibration: null,
                difficulty: 0)));
        var confidenceFirst = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MasteryCalculator.Calculate(Input(
                reasoningQuality: null,
                confidenceCalibration: 1.01m,
                difficulty: 0)));

        Assert.Equal("CurrentMastery", currentMasteryFirst.ParamName);
        Assert.Equal("ReasoningQuality", reasoningFirst.ParamName);
        Assert.Equal("TimeQuality", timeFirst.ParamName);
        Assert.Equal("ConfidenceCalibration", confidenceFirst.ParamName);
    }

    [Fact]
    public void Calculate_ResultUsesExactVersionAndConsistentRoundedValues()
    {
        var result = MasteryCalculator.Calculate(Input(reasoningQuality: 20m, difficulty: 2));

        Assert.Equal("mastery-v1", MasteryCalculator.CalculationVersion);
        Assert.Equal(MasteryCalculator.CalculationVersion, result.CalculationVersion);
        Assert.Equal(result.PreviousMastery, result.Breakdown.PreviousMastery);
        Assert.Equal(result.NewMastery, result.Breakdown.NewMastery);
        Assert.Equal(result.Delta, result.Breakdown.Delta);
        Assert.Equal(
            Math.Round(result.NewMastery - result.PreviousMastery, 2, MidpointRounding.AwayFromZero),
            result.Delta);
    }

    [Fact]
    public void Calculate_ReasoningExplanation_ContainsRequiredFacts()
    {
        var result = MasteryCalculator.Calculate(Input(currentMastery: 50m, reasoningQuality: 80m));

        Assert.NotEmpty(result.Explanation);
        Assert.True(result.Explanation.Length <= 1000);
        Assert.Contains("Reasoning path", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("50.00", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("57.50", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("+7.50", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("difficulty 3", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("80.00%", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_FallbackExplanation_ContainsReducedTrustAndTeacherReviewFacts()
    {
        var result = MasteryCalculator.Calculate(Input(
            currentMastery: 40m,
            reasoningQuality: null,
            isCorrect: false,
            timeQuality: 0m));

        Assert.NotEmpty(result.Explanation);
        Assert.True(result.Explanation.Length <= 1000);
        Assert.Contains("Fallback path", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("40.00", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("36.00", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("-4.00", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("difficulty 3", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("Reduced-trust", result.Explanation, StringComparison.Ordinal);
        Assert.Contains("teacher review", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_Explanation_IsInvariantAcrossCurrentCultures()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var frenchCultureExplanation = MasteryCalculator.Calculate(
                Input(currentMastery: 50m, reasoningQuality: 80m)).Explanation;

            CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
            var vietnameseCultureExplanation = MasteryCalculator.Calculate(
                Input(currentMastery: 50m, reasoningQuality: 80m)).Explanation;

            Assert.Equal(frenchCultureExplanation, vietnameseCultureExplanation);
            Assert.Contains("57.50", vietnameseCultureExplanation, StringComparison.Ordinal);
            Assert.DoesNotContain("57,50", vietnameseCultureExplanation, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Calculate_Explanation_DoesNotContainSensitiveSentinels()
    {
        var result = MasteryCalculator.Calculate(Input());

        Assert.DoesNotContain("raw-payload-sentinel", result.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("pii-sentinel", result.Explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-sentinel", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_RepeatedEqualInput_ReturnsEqualResultWithoutMutatingInput()
    {
        var input = Input(
            currentMastery: 33.33m,
            reasoningQuality: 72.5m,
            isCorrect: false,
            timeQuality: 0.6m,
            confidenceCalibration: 0.4m,
            difficulty: 5);
        var inputSnapshot = input with { };

        var first = MasteryCalculator.Calculate(input);
        var second = MasteryCalculator.Calculate(input);

        Assert.Equal(inputSnapshot, input);
        Assert.Equal(first, second);
        Assert.Equal(first.Breakdown, second.Breakdown);
        Assert.Equal(first.Explanation, second.Explanation);
    }

    private static MasteryCalculationInput Input(
        decimal currentMastery = 0m,
        decimal? reasoningQuality = 80m,
        bool isCorrect = true,
        decimal timeQuality = 1m,
        decimal? confidenceCalibration = 1m,
        byte difficulty = 3) =>
        new(
            CurrentMastery: currentMastery,
            ReasoningQuality: reasoningQuality,
            IsCorrect: isCorrect,
            TimeQuality: timeQuality,
            ConfidenceCalibration: confidenceCalibration,
            Difficulty: difficulty);
}
