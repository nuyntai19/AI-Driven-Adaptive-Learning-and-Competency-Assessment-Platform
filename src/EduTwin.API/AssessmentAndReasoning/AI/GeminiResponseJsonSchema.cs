using System.Text.Json.Nodes;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using Google.GenAI.Types;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GeminiResponseJsonSchema
{
    private static readonly string[] PropertyNames =
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

    public JsonObject CreateSchema()
    {
        var properties = new JsonObject
        {
            ["schemaVersion"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = CreateStringArray([AIAnalysisContract.SchemaVersion])
            },
            ["language"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = CreateStringArray(["vi", "en"])
            },
            ["methodDetected"] = new JsonObject
            {
                ["type"] = CreateStringArray(["string", "null"])
            },
            ["reasoningQuality"] = CreatePercentageSchema(),
            ["errorType"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = CreateStringArray(Enum.GetNames<ErrorType>())
            },
            ["misconception"] = new JsonObject
            {
                ["type"] = CreateStringArray(["string", "null"])
            },
            ["missingSteps"] = CreateStringArraySchema(),
            ["rootCauseNodeIds"] = CreateStringArraySchema(),
            ["confidence"] = CreatePercentageSchema(),
            ["feedback"] = new JsonObject
            {
                ["type"] = "string"
            }
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
            ["required"] = CreateStringArray(PropertyNames),
            ["propertyOrdering"] = CreateStringArray(PropertyNames)
        };
    }

    public GenerateContentConfig CreateGenerateContentConfig() =>
        new()
        {
            ResponseMimeType = "application/json",
            ResponseJsonSchema = CreateSchema(),
            CandidateCount = 1,
            Temperature = 0
        };

    private static JsonObject CreatePercentageSchema() =>
        new()
        {
            ["type"] = "integer",
            ["minimum"] = 0,
            ["maximum"] = 100
        };

    private static JsonObject CreateStringArraySchema() =>
        new()
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["type"] = "string"
            }
        };

    private static JsonArray CreateStringArray(IEnumerable<string> values) =>
        new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
}
