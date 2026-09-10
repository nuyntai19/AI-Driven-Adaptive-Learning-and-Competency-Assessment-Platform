using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public interface IEvidenceAssessmentFactory
{
    EvidenceAssessment Create(
        Attempt attempt,
        ReasoningAnalysis? analysis,
        EvidenceAssessment? supersedes,
        EvidenceGateDecision decision,
        DateTime evaluatedAt,
        Guid? createdBy);
}
