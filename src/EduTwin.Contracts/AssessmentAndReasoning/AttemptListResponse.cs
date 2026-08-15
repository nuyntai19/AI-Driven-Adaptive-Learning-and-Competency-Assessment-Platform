using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class AttemptListResponse
{
    public IReadOnlyList<AttemptSummaryDto> Data { get; set; } = [];
    public PagedMetaDto Meta { get; set; } = new();
}
