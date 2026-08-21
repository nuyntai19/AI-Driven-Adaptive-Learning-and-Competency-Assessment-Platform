namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public interface IAIService
{
    Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(
        AnalyzeReasoningRequest request,
        CancellationToken cancellationToken);
}
