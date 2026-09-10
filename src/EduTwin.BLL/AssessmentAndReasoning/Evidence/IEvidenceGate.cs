namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public interface IEvidenceGate
{
    EvidenceGateDecision Evaluate(EvidenceGateInput input);
}
