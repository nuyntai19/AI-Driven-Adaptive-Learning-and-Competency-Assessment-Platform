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
        "feedback"
    ];

    private static readonly HashSet<string> CanonicalProperties =
        new(CanonicalPropertyNames, StringComparer.Ordinal);

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
                Feedback = ReadRequiredString(root, "feedback")
            };

            _validator.Validate(request, response);
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
                || !CanonicalProperties.Contains(property.Name))
            {
                throw AIAnalysisValidationException.ShapeInvalid();
            }
        }

        if (seenProperties.Count != CanonicalPropertyNames.Length
            || CanonicalPropertyNames.Any(propertyName => !seenProperties.Contains(propertyName)))
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
