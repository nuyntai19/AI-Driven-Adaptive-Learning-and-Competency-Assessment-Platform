using System.Globalization;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed class AnalyzeReasoningResponseValidator : IAnalyzeReasoningResponseValidator
{
    private static readonly HashSet<string> SupportedLanguages =
        new(["vi", "en"], StringComparer.Ordinal);

    public void Validate(
        AnalyzeReasoningRequest request,
        AnalyzeReasoningResponse response)
    {
        if (request is null
            || response is null
            || !SupportedLanguages.Contains(request.Language)
            || request.AllowedKnowledgeNodes is null)
        {
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.RequestContext);
        }

        var allowedNodeIds = CreateAllowedNodeIdSet(request.AllowedKnowledgeNodes);

        if (!string.Equals(response.SchemaVersion, AIAnalysisContract.SchemaVersion, StringComparison.Ordinal))
        {
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.SchemaVersion);
        }

        if (!SupportedLanguages.Contains(response.Language)
            || !string.Equals(response.Language, request.Language, StringComparison.Ordinal))
        {
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Language);
        }

        if (response.ReasoningQuality is < 0 or > 100
            || response.Confidence is < 0 or > 100)
        {
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.PercentageRange);
        }

        if (!Enum.IsDefined(response.ErrorType))
        {
            throw AIAnalysisValidationException.SemanticInvalid();
        }

        ValidateText(response);
        ValidateVisualEvidence(request, response);
        ValidateCriterionDeductions(request, response);
        if (response.AnswerAssessment is not null && response.AnswerAssessment is not ("Correct" or "Incorrect" or "Uncertain"))
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Verdict);
        if (response.ReasoningVerdict is not null && response.ReasoningVerdict is not ("Valid" or "Invalid" or "Uncertain"))
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Verdict);
        ValidateReasoningConsistency(response);
        ValidateRootCauseNodeIds(response.RootCauseNodeIds, allowedNodeIds);
        if (response.SuggestedScore.HasValue)
        {
            if (response.SuggestedScore < 0 || response.SuggestedScore > request.Question.MaxScore
                || response.SuggestedScore != Math.Round(response.SuggestedScore.Value, 2))
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.ProposalRange);
            var definition = new GradingCriteria { SchemaVersion = request.Question.GradingCriteria.SchemaVersion,
                RequiredIdeas = request.Question.GradingCriteria.RequiredIdeas.ToList(), CommonErrors = request.Question.GradingCriteria.CommonErrors.ToList(),
                ScoringNotes = request.Question.GradingCriteria.ScoringNotes,
                Criteria = request.Question.GradingCriteria.Criteria.Select(c => new RubricCriterion { CriterionId = c.CriterionId, Title = c.Title, Description = c.Description, MaxScore = c.MaxScore }).ToList() };
            if (!RubricGrading.TryGrade(definition, request.Question.MaxScore, response.SuggestedRubricScores, out _, out _))
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric);
            // New grading proposals are Vietnamese, allowing quoted English exercises.
            if (response.Language != "vi" || !VietnameseFeedbackPolicy.HasVietnameseExplanation(response.Feedback)
                || (!string.IsNullOrWhiteSpace(response.AiSolution) && !VietnameseFeedbackPolicy.HasVietnameseExplanation(response.AiSolution))
                || response.SuggestedRubricScores.Any(c => !string.IsNullOrWhiteSpace(c.Comment) && !VietnameseFeedbackPolicy.HasVietnameseExplanation(c.Comment)))
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.VietnameseExplanation);
        }
        else if (response.SuggestedRubricScores.Count > 0) throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric);
    }

    private static void ValidateCriterionDeductions(AnalyzeReasoningRequest request, AnalyzeReasoningResponse response)
    {
        // Do not impose a new provider contract on ordinary batches or old checkpoints.
        if (request.VerifiedVisualEvidence is null || !response.SuggestedScore.HasValue) return;
        if (response.CriterionDeductions is not { } deductions || deductions.Count > 40)
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric, "MissingCriterionDeductionAudit");
        foreach (var deduction in deductions)
        {
            var criterion = request.Question.GradingCriteria.Criteria.SingleOrDefault(c => c.CriterionId == deduction?.CriterionId);
            var score = response.SuggestedRubricScores.FirstOrDefault(s => s.CriterionId == deduction?.CriterionId);
            if (deduction is null || criterion is null || score is null || score.AwardedScore >= criterion.MaxScore
                || deduction.EvidenceKind != (criterion.VisualRequirements is { Count: > 0 } ? "Visual" : "Nonvisual")
                || string.IsNullOrWhiteSpace(deduction.UnmetRequirement) || deduction.UnmetRequirement.Length > 1000
                || string.IsNullOrWhiteSpace(deduction.Evidence) || deduction.Evidence.Length > 1000
                || !VietnameseFeedbackPolicy.HasVietnameseExplanation(deduction.UnmetRequirement)
                || !VietnameseFeedbackPolicy.HasVietnameseExplanation(deduction.Evidence))
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric, "InvalidCriterionDeductionAudit");
        }
        foreach (var criterion in request.Question.GradingCriteria.Criteria)
        {
            var scores = response.SuggestedRubricScores.Where(s => s.CriterionId == criterion.CriterionId).ToArray();
            if (scores.Length != 1 || scores[0].AwardedScore < criterion.MaxScore && !deductions.Any(d => d.CriterionId == criterion.CriterionId))
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric, "UnexplainedCriterionDeduction");
        }
    }

    private static void ValidateVisualEvidence(AnalyzeReasoningRequest request, AnalyzeReasoningResponse response)
    {
        var visualCriteria = request.Question.GradingCriteria.Criteria.Where(c => c.VisualRequirements is { Count: > 0 }).ToArray();
        var observations = response.VisualEvidence ?? [];
        ValidateVisualObservations(request, observations);
        // An explicitly unscorable proposal may retain observations without fabricating a numeric grade.
        if (!response.SuggestedScore.HasValue) return;
        foreach (var criterion in visualCriteria)
        {
            var evidence = observations.Where(e => e.CriterionId == criterion.CriterionId).ToArray();
            var scores = response.SuggestedRubricScores.Where(s => s.CriterionId == criterion.CriterionId).ToArray();
            if (scores.Length != 1)
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric, "MissingVisualCriterionScore");
            var score = scores[0];
            // A criterion may intentionally combine visual work with calculation or
            // reasoning. Missing every visual requirement still forbids full credit,
            // but it cannot prove that the nonvisual part deserves zero. The locked
            // deduction audit records the visual loss while the teacher retains final
            // approval of the proposed partial score.
            if (evidence.Any(e => e.Status != "Present") && score.AwardedScore == criterion.MaxScore)
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric, "IncompleteVisualEvidenceWithFullScore");
        }
    }

    public static void ValidateVisualObservations(AnalyzeReasoningRequest request, IReadOnlyList<RubricVisualEvidence> observations)
    {
        var visualCriteria = request.Question.GradingCriteria.Criteria.Where(c => c.VisualRequirements is { Count: > 0 }).ToArray();
        if (observations.Count > 240)
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric);
        if (observations.Count != visualCriteria.Sum(c => c.VisualRequirements!.Count))
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric);
        var seen = new HashSet<(string, int)>();
        foreach (var evidence in observations)
        {
            var criterion = visualCriteria.SingleOrDefault(c => c.CriterionId == evidence?.CriterionId);
            if (evidence is null || criterion is null || !seen.Add((evidence.CriterionId, evidence.RequirementIndex)) ||
                evidence.RequirementIndex < 1 || evidence.RequirementIndex > criterion.VisualRequirements!.Count ||
                evidence.Status is not ("Present" or "Missing" or "Unclear") ||
                string.IsNullOrWhiteSpace(evidence.Observation) || evidence.Observation.Length > 1000 ||
                !VietnameseFeedbackPolicy.HasVietnameseExplanation(evidence.Observation) ||
                (evidence.StudentImageIndex.HasValue && (evidence.StudentImageIndex < 1 || evidence.StudentImageIndex > request.StudentSubmission.ImageParts.Count)) ||
                (evidence.Status == "Present" && !evidence.StudentImageIndex.HasValue) ||
                (request.StudentSubmission.ImageParts.Count == 0 && evidence.Status != "Missing"))
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Rubric);
        }
    }

    private static void ValidateReasoningConsistency(AnalyzeReasoningResponse response)
    {
        // Legacy checkpoints remain readable. New schemas require evidence for defects,
        // and cannot simultaneously declare the reasoning valid and identify an invalid claim.
        if (response.ReasoningIssues is not { } issues) return;
        if (issues.Count > 8 || issues.Any(i => i is null || i.Verdict is not ("Invalid" or "Uncertain")
            || string.IsNullOrWhiteSpace(i.StudentClaim) || i.StudentClaim.Length > 1000
            || string.IsNullOrWhiteSpace(i.Explanation) || i.Explanation.Length > 2000))
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.ReasoningConsistency);
        if (issues.Any(i => !VietnameseFeedbackPolicy.HasVietnameseExplanation(i.Explanation)))
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.VietnameseExplanation);
        var invalid = issues.Any(i => i.Verdict == "Invalid");
        var uncertain = issues.Any(i => i.Verdict == "Uncertain");
        var detail = invalid && response.ReasoningVerdict != "Invalid" ? "InvalidIssueVerdictMismatch"
            : !invalid && uncertain && response.ReasoningVerdict != "Uncertain" ? "UncertainIssueVerdictMismatch"
            : !invalid && !uncertain && response.ReasoningVerdict != "Valid" ? "VerdictWithoutIssueEvidence"
            : invalid && response.ErrorType == EduTwin.Contracts.AssessmentAndReasoning.ErrorType.None ? "InvalidIssueWithoutErrorType"
            : invalid && response.ReasoningQuality == 100 ? "InvalidIssueWithFullReasoningQuality"
            : response.ReasoningVerdict == "Valid" && response.AnswerAssessment == "Correct"
                && (!string.IsNullOrWhiteSpace(response.Misconception) || response.MissingSteps.Count > 0)
                    ? "CorrectValidReasoningWithConceptOrStepGap" : null;
        if (detail is not null)
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.ReasoningConsistency, detail);
    }

    private static HashSet<string> CreateAllowedNodeIdSet(
        IReadOnlyList<AnalyzeReasoningAllowedKnowledgeNode> allowedKnowledgeNodes)
    {
        var allowedNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in allowedKnowledgeNodes)
        {
            if (node is null || node.NodeId is null)
            {
                throw AIAnalysisValidationException.SemanticInvalid();
            }

            allowedNodeIds.Add(node.NodeId);
        }

        return allowedNodeIds;
    }

    private static readonly HashSet<string> AllowedSolutionTypes =
        new(["REFINED", "CORRECTED", "GENERATED", "MODEL_ANSWER"], StringComparer.Ordinal);

    private static void ValidateText(AnalyzeReasoningResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Feedback)
            || (response.MethodDetected is not null
                && (string.IsNullOrWhiteSpace(response.MethodDetected) || response.MethodDetected.Length > 500))
            || (response.Misconception is not null
                && (string.IsNullOrWhiteSpace(response.Misconception) || response.Misconception.Length > 1000))
            || response.MissingSteps is null
            || response.MissingSteps.Any(string.IsNullOrWhiteSpace)
            || (response.SolutionType is not null && !AllowedSolutionTypes.Contains(response.SolutionType)))
        {
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.Text);
        }
    }

    private static void ValidateRootCauseNodeIds(
        IReadOnlyList<string> rootCauseNodeIds,
        HashSet<string> allowedNodeIds)
    {
        if (rootCauseNodeIds is null)
        {
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.KnowledgeNodeScope);
        }

        var uniqueRootNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nodeId in rootCauseNodeIds)
        {
            if (!IsCanonicalPositiveUnsignedInteger(nodeId)
                || !uniqueRootNodeIds.Add(nodeId)
                || !allowedNodeIds.Contains(nodeId))
            {
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.KnowledgeNodeScope);
            }
        }
    }

    private static bool IsCanonicalPositiveUnsignedInteger(string? value)
    {
        if (string.IsNullOrEmpty(value) || value[0] == '0')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0;
    }
}
