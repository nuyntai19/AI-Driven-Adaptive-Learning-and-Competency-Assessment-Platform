using System.Globalization;
using System.Text.Json;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed class StrictAIAnalysisResponseParser : IAIAnalysisResponseParser
{
    private static readonly string[] CanonicalPropertyNames =
    [
        "schemaVersion",
        "language",
        "methodDetected",
        "reasoningQuality",
        "errorType",
        "misconception",
        "missingSteps",
        "rootCauseNodeIds",
        "confidence",
        "feedback",
        "solutionType",
        "aiSolution"
    ];

    private static readonly HashSet<string> CanonicalProperties =
        new(CanonicalPropertyNames, StringComparer.Ordinal);
    private static readonly HashSet<string> AdvisoryProperties = new(["answerAssessment", "reasoningVerdict", "suggestedScore", "usesAlternativeMethod", "suggestedRubricScores", "reasoningIssues", "visualEvidence", "criterionDeductions"], StringComparer.Ordinal);

    private static readonly HashSet<string> ErrorTypeNames =
        new(Enum.GetNames<ErrorType>(), StringComparer.Ordinal);

    private readonly IAnalyzeReasoningResponseValidator _validator;

    public StrictAIAnalysisResponseParser(IAnalyzeReasoningResponseValidator validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
    }

    public AnalyzeReasoningResponse ParseAndValidate(
        string rawResponse,
        AnalyzeReasoningRequest request)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            throw AIAnalysisValidationException.JsonInvalid();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                rawResponse,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
        }
        catch (JsonException)
        {
            throw AIAnalysisValidationException.JsonInvalid();
        }

        using (document)
        {
            var root = document.RootElement;
            EnsureExactObjectShape(root);

            var response = new AnalyzeReasoningResponse
            {
                SchemaVersion = ReadRequiredString(root, "schemaVersion"),
                Language = ReadRequiredString(root, "language"),
                MethodDetected = ReadNullableString(root, "methodDetected"),
                ReasoningQuality = ReadLexicalInteger(root, "reasoningQuality"),
                ErrorType = ReadErrorType(root),
                Misconception = ReadNullableString(root, "misconception"),
                MissingSteps = ReadStringArray(root, "missingSteps"),
                RootCauseNodeIds = ReadStringArray(root, "rootCauseNodeIds"),
                Confidence = ReadLexicalInteger(root, "confidence"),
                Feedback = ReadRequiredString(root, "feedback"),
                SolutionType = ReadNullableString(root, "solutionType"),
                AiSolution = ReadNullableString(root, "aiSolution"),
                AnswerAssessment = root.TryGetProperty("answerAssessment", out _) ? ReadRequiredString(root, "answerAssessment") : null,
                ReasoningVerdict = root.TryGetProperty("reasoningVerdict", out _) ? ReadRequiredString(root, "reasoningVerdict") : null,
                SuggestedScore = root.TryGetProperty("suggestedScore", out var score) && score.ValueKind != JsonValueKind.Null
                    ? score.ValueKind == JsonValueKind.Number && score.TryGetDecimal(out var number) ? number : throw AIAnalysisValidationException.ShapeInvalid() : null,
                UsesAlternativeMethod = root.TryGetProperty("usesAlternativeMethod", out var alternative)
                    ? alternative.ValueKind is JsonValueKind.True or JsonValueKind.False ? alternative.GetBoolean() : throw AIAnalysisValidationException.ShapeInvalid() : false,
                SuggestedRubricScores = ReadRubricScores(root),
                ReasoningIssues = ReadReasoningIssues(root),
                VisualEvidence = request.VerifiedVisualEvidence ?? ReadVisualEvidence(root),
                CriterionDeductions = ReadCriterionDeductions(root)
            };

            // An English exercise can have an English model answer, not English feedback.
            // If the solution is only the exact authored answer, retain that quotation
            // with a Vietnamese label and the AI's Vietnamese explanation. Do not
            // pretend to translate an arbitrary English explanation.
            var currentGradingContract = root.TryGetProperty("suggestedScore", out _);
            if (currentGradingContract && response.ReasoningIssues is null)
                throw AIAnalysisValidationException.ShapeInvalid();
            if (currentGradingContract && response.Language == "vi"
                && VietnameseFeedbackPolicy.HasVietnameseExplanation(response.Feedback)
                && !string.IsNullOrWhiteSpace(response.AiSolution)
                && !VietnameseFeedbackPolicy.HasVietnameseExplanation(response.AiSolution)
                && string.Equals(response.AiSolution.Trim(), request.Question.CorrectAnswer.Trim(), StringComparison.Ordinal))
                response = response with { AiSolution = $"Đáp án tham khảo: {response.AiSolution}\n\n{response.Feedback}" };

            // Also guard an uncertain/unscorable modern response whose score is null.
            // Legacy checkpoints retain their historical language semantics.
            if (currentGradingContract && (response.Language != "vi"
                || !VietnameseFeedbackPolicy.HasVietnameseExplanation(response.Feedback)
                || (!string.IsNullOrWhiteSpace(response.AiSolution) && !VietnameseFeedbackPolicy.HasVietnameseExplanation(response.AiSolution))))
                throw AIAnalysisValidationException.SemanticInvalid(AIResponseValidationRule.VietnameseExplanation);

            _validator.Validate(request, response);
            if (request.VerifiedVisualEvidence is not null && response.CriterionDeductions is { Count: > 0 } deductions)
                response = response with { SuggestedRubricScores = response.SuggestedRubricScores.Select(score => new RubricScoreInput {
                    CriterionId = score.CriterionId, AwardedScore = score.AwardedScore,
                    Comment = score.Comment + string.Concat(deductions.Where(d => d.CriterionId == score.CriterionId)
                        .Select(d => $"\nPhần chưa đạt: {d.UnmetRequirement} Bằng chứng: {d.Evidence}")) }).ToArray() };
            // A tested knowledge topic is not an error cause. Drop only non-error metadata
            // after validating its shape/scope; retain every genuine defect or uncertainty.
            if (response.ErrorType == ErrorType.None && response.AnswerAssessment == "Correct" && response.ReasoningVerdict == "Valid")
                response = response with { RootCauseNodeIds = [], Misconception = null };
            if (response.SuggestedScore.HasValue && request.Question.GradingCriteria.Criteria.Count > 0)
                response = response with { SuggestedScore = response.SuggestedRubricScores.Sum(s => s.AwardedScore) };
            return response;
        }
    }

    private static void EnsureExactObjectShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        var seenProperties = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in root.EnumerateObject())
        {
            if (!seenProperties.Add(property.Name)
                || (!CanonicalProperties.Contains(property.Name) && !AdvisoryProperties.Contains(property.Name)))
            {
                throw AIAnalysisValidationException.ShapeInvalid();
            }
        }

        if (CanonicalPropertyNames.Any(propertyName => !seenProperties.Contains(propertyName))
            || seenProperties.Contains("answerAssessment") != seenProperties.Contains("reasoningVerdict"))
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        var element = root.GetProperty(propertyName);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        return element.GetString()!;
    }

    private static IReadOnlyList<RubricScoreInput> ReadRubricScores(JsonElement root)
    {
        if (!root.TryGetProperty("suggestedRubricScores", out var scores)) return [];
        if (scores.ValueKind != JsonValueKind.Array) throw AIAnalysisValidationException.ShapeInvalid();
        var result = new List<RubricScoreInput>();
        foreach (var entry in scores.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || entry.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(new[] { "awardedScore", "comment", "criterionId" }) == false)
                throw AIAnalysisValidationException.ShapeInvalid();
            var value = entry.GetProperty("awardedScore");
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var score)) throw AIAnalysisValidationException.ShapeInvalid();
            result.Add(new RubricScoreInput { CriterionId = ReadRequiredString(entry, "criterionId"), AwardedScore = score, Comment = ReadNullableString(entry, "comment") });
        }
        return result;
    }

    private static IReadOnlyList<AICriterionDeduction>? ReadCriterionDeductions(JsonElement root)
    {
        if (!root.TryGetProperty("criterionDeductions", out var deductions) || deductions.ValueKind == JsonValueKind.Null) return null;
        if (deductions.ValueKind != JsonValueKind.Array || deductions.GetArrayLength() > 40)
            throw AIAnalysisValidationException.ShapeInvalid();
        return deductions.EnumerateArray().Select(d => {
            if (d.ValueKind != JsonValueKind.Object || !d.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(new[] { "criterionId", "evidence", "evidenceKind", "unmetRequirement" }))
                throw AIAnalysisValidationException.ShapeInvalid();
            return new AICriterionDeduction(ReadRequiredString(d, "criterionId"), ReadRequiredString(d, "evidenceKind"),
                ReadRequiredString(d, "unmetRequirement"), ReadRequiredString(d, "evidence"));
        }).ToArray();
    }

    private static IReadOnlyList<AIReasoningIssue>? ReadReasoningIssues(JsonElement root)
    {
        if (!root.TryGetProperty("reasoningIssues", out var issues)) return null;
        if (issues.ValueKind != JsonValueKind.Array) throw AIAnalysisValidationException.ShapeInvalid();
        var result = new List<AIReasoningIssue>();
        foreach (var issue in issues.EnumerateArray())
        {
            if (issue.ValueKind != JsonValueKind.Object || !issue.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(new[] { "explanation", "studentClaim", "verdict" }))
                throw AIAnalysisValidationException.ShapeInvalid();
            result.Add(new(ReadRequiredString(issue, "verdict"), ReadRequiredString(issue, "studentClaim"), ReadRequiredString(issue, "explanation")));
        }
        return result;
    }

    private static IReadOnlyList<RubricVisualEvidence>? ReadVisualEvidence(JsonElement root)
    {
        if (!root.TryGetProperty("visualEvidence", out var evidence)) return null;
        if (evidence.ValueKind != JsonValueKind.Array || evidence.GetArrayLength() > 240)
            throw AIAnalysisValidationException.ShapeInvalid();
        var result = new List<RubricVisualEvidence>();
        foreach (var item in evidence.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(new[] { "criterionId", "observation", "requirementIndex", "status", "studentImageIndex" }))
                throw AIAnalysisValidationException.ShapeInvalid();
            var image = item.GetProperty("studentImageIndex");
            int? imageIndex = image.ValueKind == JsonValueKind.Null ? null : ReadLexicalInteger(item, "studentImageIndex");
            result.Add(new(ReadRequiredString(item, "criterionId"), ReadLexicalInteger(item, "requirementIndex"),
                ReadRequiredString(item, "status"), imageIndex, ReadRequiredString(item, "observation")));
        }
        return result;
    }

    private static string? ReadNullableString(JsonElement root, string propertyName)
    {
        var element = root.GetProperty(propertyName);
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null => null,
            _ => throw AIAnalysisValidationException.ShapeInvalid()
        };
    }

    private static int ReadLexicalInteger(JsonElement root, string propertyName)
    {
        var element = root.GetProperty(propertyName);
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        var token = element.GetRawText();
        var digitStart = token[0] == '-' ? 1 : 0;
        if (digitStart == token.Length
            || string.Equals(token, "-0", StringComparison.Ordinal))
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        for (var index = digitStart; index < token.Length; index++)
        {
            if (token[index] is < '0' or > '9')
            {
                throw AIAnalysisValidationException.ShapeInvalid();
            }
        }

        if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        return value;
    }

    private static ErrorType ReadErrorType(JsonElement root)
    {
        var element = root.GetProperty("errorType");
        if (element.ValueKind != JsonValueKind.String)
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        var value = element.GetString()!;
        if (!ErrorTypeNames.Contains(value))
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        return Enum.Parse<ErrorType>(value, ignoreCase: false);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        var element = root.GetProperty(propertyName);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw AIAnalysisValidationException.ShapeInvalid();
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw AIAnalysisValidationException.ShapeInvalid();
            }

            values.Add(item.GetString()!);
        }

        return values;
    }
}
