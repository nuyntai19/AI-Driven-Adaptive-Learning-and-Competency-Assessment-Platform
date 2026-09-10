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
    IReadOnlyCollection<string>? DiagnosticReasonCodes = null);
