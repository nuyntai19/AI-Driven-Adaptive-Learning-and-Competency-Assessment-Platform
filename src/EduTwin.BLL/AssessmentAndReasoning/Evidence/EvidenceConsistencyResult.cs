using System.Collections.Generic;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public sealed record EvidenceConsistencyResult(
    bool SemanticValidationPassed,
    bool HasContradiction,
    bool HasAnomaly,
    bool HasRequiredEvidence,
    IReadOnlyCollection<string> ReasonCodes,
    bool StructuralValidationPassed = true);
