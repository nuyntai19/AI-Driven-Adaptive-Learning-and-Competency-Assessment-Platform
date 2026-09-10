using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public sealed record EvidenceGateDecision(
    EvidenceSourceType SourceType,
    EvidenceTrustLevel TrustLevel,
    EvidenceDecisionMode DecisionMode,
    decimal ReasoningWeight,
    IReadOnlyList<string> ReasonCodes,
    bool RequiresTeacherReview,
    string PolicyVersion,
    uint AnalysisOverrideVersion);
