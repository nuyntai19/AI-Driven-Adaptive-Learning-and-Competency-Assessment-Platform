namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class EvidenceDecisionDto
{
    public string EvidenceAssessmentId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string TrustLevel { get; set; } = string.Empty;
    public string DecisionMode { get; set; } = string.Empty;
    public decimal ReasoningWeight { get; set; }
    public IReadOnlyList<string> ReasonCodes { get; set; } = Array.Empty<string>();
    public bool RequiresTeacherReview { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public uint AnalysisOverrideVersion { get; set; }
    public DateTime EvaluatedAt { get; set; }
}
