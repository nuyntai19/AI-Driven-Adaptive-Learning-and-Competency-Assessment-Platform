namespace EduTwin.BLL.AssessmentAndReasoning.Jobs;

public enum AIAnalysisJobWorkKind
{
    Claim,
    RecoverExpiredLease
}

public sealed record AIAnalysisJobWorkItem(
    ulong AnalysisJobId,
    Guid CenterId,
    string CorrelationId,
    AIAnalysisJobWorkKind Kind,
    DateTime EligibleAt);

public sealed record AIAnalysisJobDiscoveryResult(
    IReadOnlyList<AIAnalysisJobWorkItem> WorkItems)
{
    public bool HasCandidates => WorkItems.Count > 0;
}
