using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AssessmentAndReasoning;

// A provider result, not an authoritative grade. Never expose it through student APIs.
public sealed class AIAnalysisCheckpoint : ITenantJoinEntity, IHasRowVersion
{
    public Guid CenterId { get; set; }
    public ulong AttemptId { get; set; }
    public string RequestFingerprint { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public ulong RowVersion { get; set; }
}
