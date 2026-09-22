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
            if (input.EffectiveIsCorrect.HasValue)
            {
                var reasons = input.IsPostFeedback
                    ? new[] { EvidenceReasonCodes.SourceRuleFallback, EvidenceReasonCodes.PostFeedbackAssessment }
                    : new[] { EvidenceReasonCodes.SourceRuleFallback };

                return new EvidenceGateDecision(
                    input.SourceType,
                    EvidenceTrustLevel.Reduced,
                    EvidenceDecisionMode.DeterministicOnly,
                    input.IsPostFeedback ? 0.2m : 0.3m,
                    reasons,
                    true,
                    CurrentPolicyVersion,
                    input.AnalysisOverrideVersion);
            }

            return ReviewOnly(input, EvidenceDecisionMode.DeterministicOnly, EvidenceReasonCodes.SourceRuleFallback);
        }

        if (input.SourceType == EvidenceSourceType.TeacherOverride || input.SourceType == EvidenceSourceType.TeacherApproval)
        {
            var isInvalidOverride = input.SourceType == EvidenceSourceType.TeacherOverride && input.AnalysisOverrideVersion == 0;

            if (input.EffectiveIsCorrect is null ||
                !input.StructuralValidationPassed || !input.SemanticValidationPassed || !input.HasRequiredEvidence ||
                isInvalidOverride)
            {
                var reasons = BuildValidationReasons(input);
                if (isInvalidOverride)
                {
                    reasons.Add(EvidenceReasonCodes.TeacherOverrideInvalid);
                }

                return ReviewOnly(input, EvidenceDecisionMode.HumanConfirmed, reasons);
            }

            var humanReasons = input.IsPostFeedback
                ? new[] { EvidenceReasonCodes.TeacherHumanConfirmed, EvidenceReasonCodes.PostFeedbackAssessment }
                : new[] { EvidenceReasonCodes.TeacherHumanConfirmed };

            return new EvidenceGateDecision(
                input.SourceType,
                EvidenceTrustLevel.Trusted,
                EvidenceDecisionMode.HumanConfirmed,
                input.IsPostFeedback ? 0.5m : 1m,
                humanReasons,
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
            var reasons50 = input.IsPostFeedback
                ? new[] { EvidenceReasonCodes.AIConfidence50To79, EvidenceReasonCodes.PostFeedbackAssessment }
                : new[] { EvidenceReasonCodes.AIConfidence50To79 };

            return new EvidenceGateDecision(
                input.SourceType,
                EvidenceTrustLevel.Reduced,
                EvidenceDecisionMode.AIWeighted,
                input.IsPostFeedback ? 0.25m : 0.5m,
                reasons50,
                false,
                CurrentPolicyVersion,
                input.AnalysisOverrideVersion);
        }

        var reasons80 = input.IsPostFeedback
            ? new[] { EvidenceReasonCodes.AIConfidence80To100, EvidenceReasonCodes.PostFeedbackAssessment }
            : new[] { EvidenceReasonCodes.AIConfidence80To100 };

        return new EvidenceGateDecision(
            input.SourceType,
            input.IsPostFeedback ? EvidenceTrustLevel.Reduced : EvidenceTrustLevel.Trusted,
            EvidenceDecisionMode.AIWeighted,
            input.IsPostFeedback ? 0.35m : 1m,
            reasons80,
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
