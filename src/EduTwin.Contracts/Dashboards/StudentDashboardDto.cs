using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Dashboards;

public sealed class StudentDashboardResponse
{
    public StudentDashboardDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class StudentDashboardDataDto
{
    public StudentBasicInfoDto Student { get; set; } = null!;
    public SubjectBasicInfoDto Subject { get; set; } = null!;
    public StudentGoalSummaryDto Goal { get; set; } = null!;
    public List<TopicMasteryRadarDto> MasteryRadar { get; set; } = new();
    public List<SubjectProgressPointDto> ProgressLine { get; set; } = new();
    public StudentOpportunityActionDto? Action { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public sealed class StudentBasicInfoDto
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = null!;
}

public sealed class SubjectBasicInfoDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
}

public sealed class StudentGoalSummaryDto
{
    public decimal TargetScore { get; set; }
    public uint RemainingDays { get; set; }
    public decimal CurrentPredictedScore { get; set; }
    public decimal RiskScore { get; set; }
}

public sealed class TopicMasteryRadarDto
{
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public decimal Mastery { get; set; }
}

public sealed class SubjectProgressPointDto
{
    public DateTime RecordedAt { get; set; }
    public decimal OverallSubjectMastery { get; set; }
}

public sealed class StudentOpportunityActionDto
{
    public string RecommendationId { get; set; } = null!;
    public string Strategy { get; set; } = null!;
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public string? QuestionId { get; set; }
    public decimal? OpportunityScore { get; set; }
    public string Explanation { get; set; } = null!;
}
