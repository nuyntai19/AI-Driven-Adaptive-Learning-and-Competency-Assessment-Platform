using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class TeacherReviewQueueResponse
{
    public IReadOnlyList<TeacherReviewQueueItemDto> Data { get; set; } = Array.Empty<TeacherReviewQueueItemDto>();
    public PagedMetaDto Meta { get; set; } = new();
}
