using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public sealed class EvidenceGate : IEvidenceGate
{
    public const string CurrentPolicyVersion = "evidence-gate-v1";

    public EvidenceGateDecision Evaluate(EvidenceGateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.AnalysisConfidence is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Analysis confidence must be between 0 and 100.");
        }

        if (!Enum.IsDefined(input.SourceType))
        {
            return ReviewOnly(input, EvidenceDecisionMode.AIWeighted, EvidenceReasonCodes.SourceTypeUnsupported);
        }

        if (input.SourceType == EvidenceSourceType.RuleFallback)
        {
            return ReviewOnly(input, EvidenceDecisionMode.DeterministicOnly, EvidenceReasonCodes.SourceRuleFallback);
        }

        if (input.SourceType == EvidenceSourceType.TeacherOverride)
        {
            if (input.AnalysisOverrideVersion == 0 || input.EffectiveIsCorrect is null ||
                !input.StructuralValidationPassed || !input.SemanticValidationPassed || !input.HasRequiredEvidence)
            {
                var reasons = BuildValidationReasons(input);
                if (input.AnalysisOverrideVersion == 0)
                {
                    reasons.Add(EvidenceReasonCodes.TeacherOverrideInvalid);
                }

                return ReviewOnly(input, EvidenceDecisionMode.HumanConfirmed, reasons);
            }

            return new EvidenceGateDecision(
                input.SourceType,
                EvidenceTrustLevel.Trusted,
                EvidenceDecisionMode.HumanConfirmed,
                1m,
                [EvidenceReasonCodes.TeacherHumanConfirmed],
                false,
                CurrentPolicyVersion,
                input.AnalysisOverrideVersion);
        }

        var reviewReasons = BuildValidationReasons(input);
        if (reviewReasons.Count > 0)
        {
            return ReviewOnly(input, EvidenceDecisionMode.AIWeighted, reviewReasons);
        }

        if (input.AnalysisConfidence is null)
        {
            return ReviewOnly(input, EvidenceDecisionMode.AIWeighted, EvidenceReasonCodes.AIConfidenceMissing);
        }

        if (input.AnalysisConfidence < 50m)
        {
            return ReviewOnly(input, EvidenceDecisionMode.AIWeighted, EvidenceReasonCodes.AIConfidenceBelow50);
        }

        if (input.AnalysisConfidence < 80m)
        {
            return new EvidenceGateDecision(
                input.SourceType,
                EvidenceTrustLevel.Reduced,
                EvidenceDecisionMode.AIWeighted,
                0.5m,
                [EvidenceReasonCodes.AIConfidence50To79],
                false,
                CurrentPolicyVersion,
                input.AnalysisOverrideVersion);
        }

        return new EvidenceGateDecision(
            input.SourceType,
            EvidenceTrustLevel.Trusted,
            EvidenceDecisionMode.AIWeighted,
            1m,
            [EvidenceReasonCodes.AIConfidence80To100],
            false,
            CurrentPolicyVersion,
            input.AnalysisOverrideVersion);
    }

    private static List<string> BuildValidationReasons(EvidenceGateInput input)
    {
        var reasons = new List<string>();
        if (!input.StructuralValidationPassed) reasons.Add(EvidenceReasonCodes.StructuralValidationFailed);
        if (!input.SemanticValidationPassed) reasons.Add(EvidenceReasonCodes.SemanticValidationFailed);
        if (input.HasContradiction) reasons.Add(EvidenceReasonCodes.ContradictionDetected);
        if (input.HasAnomaly) reasons.Add(EvidenceReasonCodes.AnomalyDetected);
        if (!input.HasRequiredEvidence) reasons.Add(EvidenceReasonCodes.RequiredEvidenceMissing);
        if (input.EffectiveIsCorrect is null) reasons.Add(EvidenceReasonCodes.PreliminaryCorrectnessPending);

        if (input.DiagnosticReasonCodes is not null)
        {
            foreach (var code in input.DiagnosticReasonCodes)
            {
                if (!string.IsNullOrWhiteSpace(code) && !reasons.Contains(code))
                {
                    reasons.Add(code);
                }
            }
        }

        return reasons;
    }

    private static EvidenceGateDecision ReviewOnly(
        EvidenceGateInput input,
        EvidenceDecisionMode mode,
        params string[] reasons) => ReviewOnly(input, mode, (IReadOnlyList<string>)reasons);

    private static EvidenceGateDecision ReviewOnly(
        EvidenceGateInput input,
        EvidenceDecisionMode mode,
        IReadOnlyList<string> reasons) => new(
            input.SourceType,
            EvidenceTrustLevel.ReviewOnly,
            mode,
            0m,
            reasons,
            true,
            CurrentPolicyVersion,
            input.AnalysisOverrideVersion);
}
