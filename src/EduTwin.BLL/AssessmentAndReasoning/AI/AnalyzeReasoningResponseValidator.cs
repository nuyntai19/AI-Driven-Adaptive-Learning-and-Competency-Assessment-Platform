using System.Globalization;

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
            throw AIAnalysisValidationException.SemanticInvalid();
        }

        var allowedNodeIds = CreateAllowedNodeIdSet(request.AllowedKnowledgeNodes);

        if (!string.Equals(response.SchemaVersion, AIAnalysisContract.SchemaVersion, StringComparison.Ordinal))
        {
            throw AIAnalysisValidationException.SemanticInvalid();
        }

        if (!SupportedLanguages.Contains(response.Language)
            || !string.Equals(response.Language, request.Language, StringComparison.Ordinal))
        {
            throw AIAnalysisValidationException.SemanticInvalid();
        }

        if (response.ReasoningQuality is < 0 or > 100
            || response.Confidence is < 0 or > 100)
        {
            throw AIAnalysisValidationException.SemanticInvalid();
        }

        if (!Enum.IsDefined(response.ErrorType))
        {
            throw AIAnalysisValidationException.SemanticInvalid();
        }

        ValidateText(response);
        ValidateRootCauseNodeIds(response.RootCauseNodeIds, allowedNodeIds);
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

    private static void ValidateText(AnalyzeReasoningResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.Feedback)
            || (response.MethodDetected is not null
                && (string.IsNullOrWhiteSpace(response.MethodDetected) || response.MethodDetected.Length > 500))
            || (response.Misconception is not null
                && (string.IsNullOrWhiteSpace(response.Misconception) || response.Misconception.Length > 1000))
            || response.MissingSteps is null
            || response.MissingSteps.Any(string.IsNullOrWhiteSpace))
        {
            throw AIAnalysisValidationException.SemanticInvalid();
        }
    }

    private static void ValidateRootCauseNodeIds(
        IReadOnlyList<string> rootCauseNodeIds,
        HashSet<string> allowedNodeIds)
    {
        if (rootCauseNodeIds is null)
        {
            throw AIAnalysisValidationException.SemanticInvalid();
        }

        var uniqueRootNodeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nodeId in rootCauseNodeIds)
        {
            if (!IsCanonicalPositiveUnsignedInteger(nodeId)
                || !uniqueRootNodeIds.Add(nodeId)
                || !allowedNodeIds.Contains(nodeId))
            {
                throw AIAnalysisValidationException.SemanticInvalid();
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
