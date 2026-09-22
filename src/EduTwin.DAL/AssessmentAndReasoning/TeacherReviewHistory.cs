using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.DAL.AssessmentAndReasoning;

public class TeacherReviewHistory : ITenantAppendOnlyEntity, IHasRowVersion
{
    public ulong HistoryId { get; set; }
    public Guid CenterId { get; set; }
    public ulong AnalysisId { get; set; }
    public ulong AttemptId { get; set; }
    public Guid TeacherId { get; set; }
    public string Decision { get; set; } = null!; // Approved | Adjusted
    public decimal? PreviousScore { get; set; }
    public decimal? NewScore { get; set; }
    public bool? PreviousIsCorrect { get; set; }
    public bool? NewIsCorrect { get; set; }
    public string? Note { get; set; }
    public uint OverrideVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public ulong RowVersion { get; set; }

    public ReasoningAnalysis Analysis { get; set; } = null!;
    public Attempt Attempt { get; set; } = null!;
    public User Teacher { get; set; } = null!;
}
