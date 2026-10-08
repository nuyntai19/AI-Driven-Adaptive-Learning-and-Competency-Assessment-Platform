using System.Globalization;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.Contracts.CurriculumAndQuestions;

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
        if ((invalid && response.ReasoningVerdict != "Invalid")
            || (!invalid && uncertain && response.ReasoningVerdict != "Uncertain")
            || (!invalid && !uncertain && response.ReasoningVerdict != "Valid")
            || (invalid && response.ErrorType == EduTwin.Contracts.AssessmentAndReasoning.ErrorType.None)
            || (invalid && response.ReasoningQuality == 100)
            || (response.ReasoningVerdict == "Valid" && response.AnswerAssessment == "Correct"
                && (!string.IsNullOrWhiteSpace(response.Misconception) || response.MissingSteps.Count > 0)))
            throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.ReasoningConsistency);
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
