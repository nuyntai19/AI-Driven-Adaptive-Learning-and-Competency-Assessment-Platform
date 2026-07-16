using System;
using System.Text.Json;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.Contracts.DigitalTwin;

namespace EduTwin.DAL.DigitalTwin;

public class TwinUpdateHistory : ITenantAppendOnlyEntity
{
    public ulong HistoryId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SubjectId { get; set; }
    public ulong TopicNodeId { get; set; }
    public ulong? AttemptId { get; set; }
    public ulong? AnalysisId { get; set; }
    public TwinEventSource EventSource { get; set; }
    public decimal PreviousMastery { get; set; }
    public decimal NewMastery { get; set; }
    public decimal MasteryDelta { get; set; }
    public decimal? EffectiveReasoningQuality { get; set; }
    public string CalculationVersion { get; set; } = null!;
    public JsonDocument CalculationBreakdown { get; set; } = null!;
    public string Explanation { get; set; } = null!;

    public Guid CenterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public Student Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public KnowledgeNode TopicNode { get; set; } = null!;
}
