using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AssessmentAndReasoning;

public sealed class AIStudentPostProcessingJob : ITenantJoinEntity, IHasRowVersion
{
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    // Guid.Empty identifies free practice; assignment work is coalesced separately.
    public Guid AssignmentScopeId { get; set; }
    public ulong Revision { get; set; }
    public ulong ProcessedRevision { get; set; }
    public ulong SourceAttemptId { get; set; }
    public DateTime TriggerAt { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public int FailureCount { get; set; }
    public string? LastErrorCode { get; set; }
    public ulong RowVersion { get; set; }
}
