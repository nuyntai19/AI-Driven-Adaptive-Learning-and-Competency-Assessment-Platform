namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public interface IAnalyzeReasoningResponseValidator
{
    void Validate(
        AnalyzeReasoningRequest request,
        AnalyzeReasoningResponse response);
}
