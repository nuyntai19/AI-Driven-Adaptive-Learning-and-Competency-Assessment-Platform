using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public sealed class LearningPathTopicNodeDto
{
    public string TopicNodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public decimal CurrentMastery { get; set; }
    public uint EvidenceCount { get; set; }
    public string? Description { get; set; }
}

public sealed class LearningPathTopicsResponse
{
    public List<LearningPathTopicNodeDto> Data { get; set; } = new();
    public MetaDto Meta { get; set; } = null!;
}
