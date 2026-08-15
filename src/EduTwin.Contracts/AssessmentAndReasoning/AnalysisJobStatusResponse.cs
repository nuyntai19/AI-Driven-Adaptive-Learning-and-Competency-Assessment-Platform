using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class AnalysisJobStatusResponse
{
    public required AnalysisJobStatusDataDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}
