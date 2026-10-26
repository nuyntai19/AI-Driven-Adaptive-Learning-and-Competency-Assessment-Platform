using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public sealed record EvidenceGateInput(
    EvidenceSourceType SourceType,
    bool StructuralValidationPassed,
    bool SemanticValidationPassed,
    bool HasContradiction,
    bool HasAnomaly,
    bool HasRequiredEvidence,
    bool? EffectiveIsCorrect,
    decimal? AnalysisConfidence,
    uint AnalysisOverrideVersion,
    IReadOnlyCollection<string>? DiagnosticReasonCodes = null,
    bool IsPostFeedback = false,
    // True when the score itself was produced by exact auto-grading (text/numeric
    // match), so correctness does not depend on the reasoning-analysis provider.
    bool CorrectnessIsDeterministic = false);
