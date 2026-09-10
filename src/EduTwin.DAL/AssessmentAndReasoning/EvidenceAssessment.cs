using System.Text.Json;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AssessmentAndReasoning;

public sealed class EvidenceAssessment : ITenantAppendOnlyEntity
{
    public ulong EvidenceAssessmentId { get; set; }
    public Guid CenterId { get; set; }
    public ulong AttemptId { get; set; }
    public ulong? AnalysisId { get; set; }
    public ulong? SupersedesAssessmentId { get; set; }
    public EvidenceSourceType SourceType { get; set; }
    public EvidenceTrustLevel TrustLevel { get; set; }
    public EvidenceDecisionMode DecisionMode { get; set; }
    public decimal ReasoningWeight { get; set; }
    public JsonDocument ReasonCodes { get; set; } = null!;
    public bool RequiresTeacherReview { get; set; }
    public string PolicyVersion { get; set; } = null!;
    public uint AnalysisOverrideVersion { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }

    public Attempt Attempt { get; set; } = null!;
    public ReasoningAnalysis? Analysis { get; set; }
    public EvidenceAssessment? SupersedesAssessment { get; set; }
}
