using System;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class TeacherOverrideResponse
{
    public TeacherOverrideDataDto Data { get; set; } = new();
    public MetaDto Meta { get; set; } = null!;
}

public sealed class TeacherOverrideDataDto
{
    public string AnalysisId { get; set; } = string.Empty;
    public bool HasTeacherOverride { get; set; }
    public uint OverrideVersion { get; set; }
    public DateTime OverriddenAt { get; set; }
    public decimal? OverrideAwardedScore { get; set; }
    public decimal? EffectiveAwardedScore { get; set; }
    public TeacherOverrideReplayDto Replay { get; set; } = new();
}

public sealed class TeacherOverrideReplayDto
{
    public string StudentId { get; set; } = string.Empty;
    public string TopicNodeId { get; set; } = string.Empty;
    public int AttemptsReplayed { get; set; }
    public decimal PreviousMastery { get; set; }
    public decimal NewMastery { get; set; }
    public decimal NewRiskScore { get; set; }
    public bool RecommendationRecalculated { get; set; }
}
