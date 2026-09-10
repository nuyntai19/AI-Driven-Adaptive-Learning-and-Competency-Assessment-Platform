using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class TeacherReviewQueueQuery
{
    public Guid? ClassId { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
