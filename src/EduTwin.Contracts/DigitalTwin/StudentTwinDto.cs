using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.DigitalTwin;

public sealed class StudentTwinResponse
{
    public StudentTwinDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class StudentTwinDataDto
{
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public List<TopicTwinNodeDto> Topics { get; set; } = new();
    public BehaviorTwinSummaryDto Behavior { get; set; } = null!;
}

public sealed class TopicTwinNodeDto
{
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public decimal MasteryPercentage { get; set; }
    public uint EvidenceCount { get; set; }
    public decimal? LastReasoningQuality { get; set; }
    public string? LastAttemptId { get; set; }
}

public sealed class BehaviorTwinSummaryDto
{
    public decimal AvgTimeSpentSeconds { get; set; }
    public decimal SkipRate { get; set; }
    public decimal ChangeAnswerRate { get; set; }
    public decimal AvgConfidence { get; set; }
    public decimal ConfidenceCalibration { get; set; }
    public uint AttemptCount { get; set; }
}

public sealed class TwinHistoryResponse
{
    public List<TwinHistoryItemDto> Data { get; set; } = new();
    public MetaDto Meta { get; set; } = null!;
}

public sealed class TwinHistoryItemDto
{
    public string HistoryId { get; set; } = null!;
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public string EventSource { get; set; } = null!;
    public decimal PreviousMastery { get; set; }
    public decimal NewMastery { get; set; }
    public decimal Delta { get; set; }
    public decimal? ReasoningQuality { get; set; }
    public string Explanation { get; set; } = null!;
    public DateTime RecordedAt { get; set; }
}
