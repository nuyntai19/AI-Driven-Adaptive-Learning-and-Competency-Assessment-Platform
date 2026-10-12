namespace EduTwin.BLL.AssessmentAndReasoning.AI;

// Execution metadata lives outside the exact provider request/response contract.
public sealed record AIAnalysisBatchPartition(Guid CenterId, Guid StudentId, Guid AssignmentId);

public interface IPartitionedAIService
{
    Task<AnalyzeReasoningResponse> AnalyzeReasoningAsync(AnalyzeReasoningRequest request,
        AIAnalysisBatchPartition partition, CancellationToken cancellationToken);
}

public interface IAIAnalysisProvenance
{
    string ProviderName { get; }
    string ModelName { get; }
}
