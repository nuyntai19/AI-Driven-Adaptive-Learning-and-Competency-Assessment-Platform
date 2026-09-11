using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Dashboards;

public sealed class ClassDashboardResponse
{
    public ClassDashboardDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class ClassDashboardDataDto
{
    public ClassBasicInfoDto Class { get; set; } = null!;
    public ClassOverviewDto Overview { get; set; } = null!;
    public List<ClassHighRiskStudentDto> HighRiskStudents { get; set; } = new();
    public List<ClassWeakTopicDto> WeakTopics { get; set; } = new();
    public List<ClassGapGroupDto> GapGroups { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public sealed class ClassBasicInfoDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
}

public sealed class ClassOverviewDto
{
    public int StudentCount { get; set; }
    public decimal AveragePredictedScore { get; set; }
    public decimal AverageMastery { get; set; }
    public decimal AssignmentCompletionRate { get; set; }
}

public sealed class ClassHighRiskStudentDto
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = null!;
    public decimal TargetScore { get; set; }
    public decimal PredictedScore { get; set; }
    public uint RemainingDays { get; set; }
    public decimal RiskScore { get; set; }
}

public sealed class ClassWeakTopicDto
{
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public decimal AverageMastery { get; set; }
    public int AffectedStudentCount { get; set; }
}

public sealed class ClassGapGroupDto
{
    public string GroupKey { get; set; } = null!;
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public decimal Threshold { get; set; }
    public int StudentCount { get; set; }
    public List<Guid> StudentIds { get; set; } = new();
    public string SuggestedAction { get; set; } = null!;
}
