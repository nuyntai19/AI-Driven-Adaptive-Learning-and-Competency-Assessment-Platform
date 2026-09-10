using System.Text.Json;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public sealed class EvidenceAssessmentFactory : IEvidenceAssessmentFactory
{
    public EvidenceAssessment Create(
        Attempt attempt,
        ReasoningAnalysis? analysis,
        EvidenceAssessment? supersedes,
        EvidenceGateDecision decision,
        DateTime evaluatedAt,
        Guid? createdBy)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(decision);

        if (attempt.CenterId == Guid.Empty || attempt.AttemptId == 0)
        {
            throw new ArgumentException("A persisted tenant-bound attempt is required.", nameof(attempt));
        }

        if (analysis is not null &&
            (analysis.CenterId != attempt.CenterId || analysis.AttemptId != attempt.AttemptId))
        {
            throw new InvalidOperationException("Evidence analysis must belong to the same center and attempt.");
        }

        if (supersedes is not null &&
            (supersedes.EvidenceAssessmentId == 0 ||
             supersedes.CenterId != attempt.CenterId ||
             supersedes.AttemptId != attempt.AttemptId))
        {
            throw new InvalidOperationException(
                "Superseded evidence must be persisted and belong to the same center and attempt.");
        }

        if (supersedes is not null &&
            (supersedes.CenterId != attempt.CenterId || supersedes.AttemptId != attempt.AttemptId))
        {
            throw new InvalidOperationException("Superseded evidence must belong to the same center and attempt.");
        }

        return new EvidenceAssessment
        {
            CenterId = attempt.CenterId,
            AttemptId = attempt.AttemptId,
            Analysis = analysis,
            AnalysisId = analysis != null && analysis.AnalysisId > 0 ? analysis.AnalysisId : null,
            SupersedesAssessment = supersedes,
            SupersedesAssessmentId = supersedes?.EvidenceAssessmentId,
            SourceType = decision.SourceType,
            TrustLevel = decision.TrustLevel,
            DecisionMode = decision.DecisionMode,
            ReasoningWeight = decision.ReasoningWeight,
            ReasonCodes = JsonSerializer.SerializeToDocument(decision.ReasonCodes),
            RequiresTeacherReview = decision.RequiresTeacherReview,
            PolicyVersion = decision.PolicyVersion,
            AnalysisOverrideVersion = decision.AnalysisOverrideVersion,
            EvaluatedAt = evaluatedAt,
            CreatedAt = evaluatedAt,
            CreatedBy = createdBy
        };
    }
}
