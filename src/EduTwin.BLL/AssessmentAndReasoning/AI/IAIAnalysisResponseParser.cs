namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public interface IAIAnalysisResponseParser
{
    AnalyzeReasoningResponse ParseAndValidate(
        string rawResponse,
        AnalyzeReasoningRequest request);
}
