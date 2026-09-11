using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public sealed class LearningPathItemDto
{
    public ulong LearningPathItemId { get; set; }
    public ulong TopicNodeId { get; set; }
    public string TopicName { get; set; } = null!;
    public ulong? RecommendedQuestionId { get; set; }
    public uint RankOrder { get; set; }
    public decimal? OpportunityScore { get; set; }
    public string Reason { get; set; } = null!;
    public LearningPathItemStatus Status { get; set; }
}

public sealed class LearningPathDto
{
    public Guid LearningPathId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public LearningPathStrategy Strategy { get; set; }
    public uint Version { get; set; }
    public LearningPathStatus Status { get; set; }
    public DateTime GeneratedAt { get; set; }
    public IReadOnlyList<LearningPathItemDto> Items { get; set; } = Array.Empty<LearningPathItemDto>();
}

public sealed class LearningPathResponse
{
    public LearningPathDto? Data { get; set; }
    public MetaDto Meta { get; set; } = null!;
}
