using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AssessmentAndReasoning;

// Provider capacity is shared across tenants. Contains counters/leases, never API keys or submissions.
public sealed class AIProviderQuotaState : IHasRowVersion
{
    public string PoolId { get; set; } = null!;
    public string StateJson { get; set; } = "{}";
    public ulong RowVersion { get; set; }
}
