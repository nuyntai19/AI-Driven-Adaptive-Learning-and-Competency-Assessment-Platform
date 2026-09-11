using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

/// <summary>
/// Durable ordering watermark for recommendation-generation triggers.
/// One row exists per tenant/student/subject, including when the latest
/// generation produced no recommendation.
/// </summary>
public sealed class RecommendationGenerationState : ITenantJoinEntity, IHasRowVersion
{
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public DateTime LastTriggerAt { get; set; }
    public ulong? LastSourceAttemptId { get; set; }
    public string LastOutcome { get; set; } = null!;
    public string? DiagnosticReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ulong RowVersion { get; set; }

    public Student Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
}
