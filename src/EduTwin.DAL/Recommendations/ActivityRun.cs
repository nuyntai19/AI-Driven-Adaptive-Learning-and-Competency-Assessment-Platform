using System;
using System.Collections.Generic;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Recommendations;

public class ActivityRun : IMutableTenantAggregate
{
    public ulong ActivityRunId { get; set; }
    public Guid CenterId { get; set; }
    public ulong SessionRunId { get; set; }
    public Guid? ContentVersionId { get; set; }

    public string ActivityType { get; set; } = "WorkedExample";
    public uint ActivityOrder { get; set; } = 1;
    public string Status { get; set; } = "NotStarted"; // NotStarted, InProgress, Completed, NeedsReview

    public string? DraftPayload { get; set; }
    public uint DraftRevision { get; set; } = 1;

    public uint AssistanceRequestedCount { get; set; } = 0;
    public string AssistanceMode { get; set; } = "Normal"; // Normal, Blocked, HintOnly

    public decimal? Score { get; set; }
    public decimal? MaxScore { get; set; }
    public bool? IsMastered { get; set; }
    public string? Feedback { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; } = 1;

    public LearningPathSessionRun SessionRun { get; set; } = null!;
    public LearningContentVersion? ContentVersion { get; set; }
    public ICollection<IssuedItem> IssuedItems { get; set; } = new List<IssuedItem>();
}
