using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Dashboards;

public sealed class CenterDashboardResponse
{
    public CenterDashboardDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class CenterDashboardDataDto
{
    public CenterDashboardSummaryDto Summary { get; set; } = null!;
    public List<SubjectMasterySummaryDto> MasteryBySubject { get; set; } = new();
    public List<ClassHighRiskSummaryDto> HighRiskByClass { get; set; } = new();
    public List<ClassRankingItemDto> ClassRanking { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public sealed class CenterDashboardSummaryDto
{
    public int TeacherCount { get; set; }
    public int StudentCount { get; set; }
    public int ClassCount { get; set; }
}

public sealed class SubjectMasterySummaryDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = null!;
    public decimal AverageMastery { get; set; }
}

public sealed class ClassHighRiskSummaryDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public int HighRiskStudentCount { get; set; }
    public int TotalStudentCount { get; set; }
}

public sealed class ClassRankingItemDto
{
    public int Rank { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public string SubjectName { get; set; } = null!;
    public decimal AverageMastery { get; set; }
    public decimal AssignmentCompletionRate { get; set; }
}
