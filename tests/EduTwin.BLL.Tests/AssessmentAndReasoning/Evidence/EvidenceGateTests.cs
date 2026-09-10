using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.Contracts.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Evidence;

public sealed class EvidenceGateTests
{
    [Theory]
    [InlineData("80", EvidenceTrustLevel.Trusted, "1", false, EvidenceReasonCodes.AIConfidence80To100)]
    [InlineData("100", EvidenceTrustLevel.Trusted, "1", false, EvidenceReasonCodes.AIConfidence80To100)]
    [InlineData("50", EvidenceTrustLevel.Reduced, "0.5", false, EvidenceReasonCodes.AIConfidence50To79)]
    [InlineData("79.99", EvidenceTrustLevel.Reduced, "0.5", false, EvidenceReasonCodes.AIConfidence50To79)]
    [InlineData("49.99", EvidenceTrustLevel.ReviewOnly, "0", true, EvidenceReasonCodes.AIConfidenceBelow50)]
    public void Evaluate_AIConfidenceBoundary_UsesDocumentedTrustPolicy(
        string confidenceText,
        EvidenceTrustLevel expectedTrust,
        string expectedWeightText,
        bool expectedReview,
        string expectedReason)
    {
        var result = CreateGate().Evaluate(ValidAI(decimal.Parse(
            confidenceText,
            System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(expectedTrust, result.TrustLevel);
        Assert.Equal(decimal.Parse(expectedWeightText, System.Globalization.CultureInfo.InvariantCulture), result.ReasoningWeight);
        Assert.Equal(expectedReview, result.RequiresTeacherReview);
        Assert.Equal([expectedReason], result.ReasonCodes);
        Assert.Equal(EvidenceDecisionMode.AIWeighted, result.DecisionMode);
        Assert.Equal(EvidenceGate.CurrentPolicyVersion, result.PolicyVersion);
    }

    [Fact]
    public void Evaluate_NullCorrectness_IsAlwaysReviewOnly()
    {
        var result = CreateGate().Evaluate(ValidAI(95m) with { EffectiveIsCorrect = null });

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.TrustLevel);
        Assert.Equal(0m, result.ReasoningWeight);
        Assert.True(result.RequiresTeacherReview);
        Assert.Contains(EvidenceReasonCodes.PreliminaryCorrectnessPending, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_RuleFallback_IsAlwaysReviewOnly()
    {
        var result = CreateGate().Evaluate(ValidAI(100m) with
        {
            SourceType = EvidenceSourceType.RuleFallback
        });

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.TrustLevel);
        Assert.Equal(EvidenceDecisionMode.DeterministicOnly, result.DecisionMode);
        Assert.Equal(0m, result.ReasoningWeight);
        Assert.Equal([EvidenceReasonCodes.SourceRuleFallback], result.ReasonCodes);
        Assert.True(result.RequiresTeacherReview);
    }

    [Theory]
    [InlineData(false, true, false, false, true, EvidenceReasonCodes.StructuralValidationFailed)]
    [InlineData(true, false, false, false, true, EvidenceReasonCodes.SemanticValidationFailed)]
    [InlineData(true, true, true, false, true, EvidenceReasonCodes.ContradictionDetected)]
    [InlineData(true, true, false, true, true, EvidenceReasonCodes.AnomalyDetected)]
    [InlineData(true, true, false, false, false, EvidenceReasonCodes.RequiredEvidenceMissing)]
    public void Evaluate_UnsafeAIInput_FailsClosed(
        bool structural,
        bool semantic,
        bool contradiction,
        bool anomaly,
        bool requiredEvidence,
        string expectedReason)
    {
        var result = CreateGate().Evaluate(ValidAI(99m) with
        {
            StructuralValidationPassed = structural,
            SemanticValidationPassed = semantic,
            HasContradiction = contradiction,
            HasAnomaly = anomaly,
            HasRequiredEvidence = requiredEvidence
        });

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.TrustLevel);
        Assert.Equal(0m, result.ReasoningWeight);
        Assert.Contains(expectedReason, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_TeacherOverrideWithVersionAndCompleteEvidence_IsHumanConfirmed()
    {
        var result = CreateGate().Evaluate(ValidAI(null) with
        {
            SourceType = EvidenceSourceType.TeacherOverride,
            AnalysisOverrideVersion = 2
        });

        Assert.Equal(EvidenceTrustLevel.Trusted, result.TrustLevel);
        Assert.Equal(EvidenceDecisionMode.HumanConfirmed, result.DecisionMode);
        Assert.Equal(1m, result.ReasoningWeight);
        Assert.False(result.RequiresTeacherReview);
        Assert.Equal([EvidenceReasonCodes.TeacherHumanConfirmed], result.ReasonCodes);
        Assert.Equal(2u, result.AnalysisOverrideVersion);
    }

    [Fact]
    public void Evaluate_TeacherOverrideWithoutVersion_FailsClosed()
    {
        var result = CreateGate().Evaluate(ValidAI(100m) with
        {
            SourceType = EvidenceSourceType.TeacherOverride,
            AnalysisOverrideVersion = 0
        });

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.TrustLevel);
        Assert.Equal(0m, result.ReasoningWeight);
        Assert.True(result.RequiresTeacherReview);
        Assert.Contains(EvidenceReasonCodes.TeacherOverrideInvalid, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_UnsupportedSourceType_FailsClosedWithReason()
    {
        var result = CreateGate().Evaluate(ValidAI(100m) with
        {
            SourceType = (EvidenceSourceType)999
        });

        Assert.Equal(EvidenceTrustLevel.ReviewOnly, result.TrustLevel);
        Assert.Equal(0m, result.ReasoningWeight);
        Assert.True(result.RequiresTeacherReview);
        Assert.Equal([EvidenceReasonCodes.SourceTypeUnsupported], result.ReasonCodes);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    public void Evaluate_OutOfRangeConfidence_Throws(string confidenceText)
    {
        var confidence = decimal.Parse(confidenceText, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateGate().Evaluate(ValidAI(confidence)));
    }

    private static EvidenceGate CreateGate() => new();

    private static EvidenceGateInput ValidAI(decimal? confidence) => new(
        EvidenceSourceType.AI,
        StructuralValidationPassed: true,
        SemanticValidationPassed: true,
        HasContradiction: false,
        HasAnomaly: false,
        HasRequiredEvidence: true,
        EffectiveIsCorrect: true,
        AnalysisConfidence: confidence,
        AnalysisOverrideVersion: 0);
}
