using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public static class EvidenceProjectionMapper
{
    public static EvidenceDecisionDto Map(EvidenceAssessment evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var reasonCodes = evidence.ReasonCodes.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
            ? evidence.ReasonCodes.RootElement.EnumerateArray()
                .Where(item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToArray()
            : Array.Empty<string>();

        return new EvidenceDecisionDto
        {
            EvidenceAssessmentId = evidence.EvidenceAssessmentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SourceType = evidence.SourceType.ToString(),
            TrustLevel = evidence.TrustLevel.ToString(),
            DecisionMode = evidence.DecisionMode.ToString(),
            ReasoningWeight = evidence.ReasoningWeight,
            ReasonCodes = reasonCodes,
            RequiresTeacherReview = evidence.RequiresTeacherReview,
            PolicyVersion = evidence.PolicyVersion,
            AnalysisOverrideVersion = evidence.AnalysisOverrideVersion,
            EvaluatedAt = evidence.EvaluatedAt
        };
    }
}
