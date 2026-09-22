using System;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class TeacherApproveDataDto
{
    public string AnalysisId { get; set; } = string.Empty;
    public string AttemptId { get; set; } = string.Empty;
    public string ReviewDecision { get; set; } = TeacherReviewDecision.Approved;
    public bool NeedsTeacherReview { get; set; }
    public uint OverrideVersion { get; set; }
    public DateTime ReviewedAt { get; set; }
    public string? TeacherNote { get; set; }
    public decimal EffectiveAwardedScore { get; set; }
    public bool EffectiveIsCorrect { get; set; }
    public TeacherOverrideReplayDto Replay { get; set; } = new();
}

public sealed class TeacherApproveResponse
{
    public TeacherApproveDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
