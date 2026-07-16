using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.DAL.AssessmentAndReasoning;

public class AIAnalysisJob : ITenantAppendOnlyEntity, IHasRowVersion
{
    public ulong AnalysisJobId { get; set; }
    public Guid CenterId { get; set; }
    public ulong AttemptId { get; set; }
    public AIJobStatus Status { get; set; }
    public byte RetryCount { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public string CorrelationId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ulong RowVersion { get; set; }

    public Attempt Attempt { get; set; } = null!;
}