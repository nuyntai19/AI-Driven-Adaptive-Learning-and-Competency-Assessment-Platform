using System;
using System.Text.Json;
using EduTwin.DAL.Persistence.Models;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.AssessmentAndReasoning;

public class ReasoningAnalysis : ITenantAppendOnlyEntity, IHasRowVersion
{
    public ulong AnalysisId { get; set; }
    public Guid CenterId { get; set; }
    public ulong AttemptId { get; set; }
    public string SchemaVersion { get; set; } = null!;
    public string? MethodDetected { get; set; }
    public decimal? ReasoningQuality { get; set; }
    public ErrorType ErrorType { get; set; }
    public string? Misconception { get; set; }
    public JsonDocument MissingSteps { get; set; } = null!;
    public JsonDocument RootCauseNodeIds { get; set; } = null!;
    public decimal? AnalysisConfidence { get; set; }
    public string Feedback { get; set; } = null!;
    public bool IsFallback { get; set; }
    public bool NeedsTeacherReview { get; set; }
    public AnalysisProvider Provider { get; set; }
    public string? ModelName { get; set; }

    public decimal? OverrideReasoningQuality { get; set; }
    public ErrorType? OverrideErrorType { get; set; }
    public string? OverrideFeedback { get; set; }
    public bool? OverrideIsCorrect { get; set; }
    public string? OverrideReason { get; set; }
    public Guid? OverriddenByTeacherId { get; set; }
    public DateTime? OverriddenAt { get; set; }
    public uint OverrideVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ulong RowVersion { get; set; }

    public Attempt Attempt { get; set; } = null!;
    public Teacher? OverriddenByTeacher { get; set; }
}
