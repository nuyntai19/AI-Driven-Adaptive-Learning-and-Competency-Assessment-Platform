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
        "visualEvidence",
        "methodDetected",
        "reasoningQuality",
        "errorType",
        "misconception",
        "missingSteps",
        "rootCauseNodeIds",
        "confidence",
        "feedback",
        "solutionType",
        "aiSolution",
        "answerAssessment",
        "reasoningVerdict",
        "suggestedScore",
        "usesAlternativeMethod",
        "suggestedRubricScores",
        "reasoningIssues"
    ];

    public JsonObject CreateSchema(bool requireCriterionDeductions = false)
    {
        var properties = new JsonObject
        {
            // Do not put the application's large safety cap in the provider schema:
            // maxItems=240 expands Gemini's structured-output grammar and is rejected
            // with HTTP 400. The strict parser and per-request validator enforce the
            // cap and exact authored requirement count after generation instead.
            ["visualEvidence"] = new JsonObject { ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = false,
                    ["required"] = CreateStringArray(["criterionId", "requirementIndex", "status", "studentImageIndex", "observation"]),
                    ["properties"] = new JsonObject {
                        ["criterionId"] = new JsonObject { ["type"] = "string" },
                        ["requirementIndex"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["status"] = new JsonObject { ["type"] = "string", ["enum"] = CreateStringArray(["Present", "Missing", "Unclear"]) },
                        ["studentImageIndex"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["nullable"] = true },
                        ["observation"] = new JsonObject { ["type"] = "string" } } } },
            ["reasoningIssues"] = new JsonObject { ["type"] = "array", ["maxItems"] = 8,
                ["items"] = new JsonObject { ["type"] = "object", ["additionalProperties"] = false,
                    ["required"] = CreateStringArray(["verdict", "studentClaim", "explanation"]),
                    ["properties"] = new JsonObject {
                        ["verdict"] = new JsonObject { ["type"] = "string", ["enum"] = CreateStringArray(["Invalid", "Uncertain"]) },
                        ["studentClaim"] = new JsonObject { ["type"] = "string" },
                        ["explanation"] = new JsonObject { ["type"] = "string" } } } },
            ["suggestedScore"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["nullable"] = true },
            ["usesAlternativeMethod"] = new JsonObject { ["type"] = "boolean" },
            ["suggestedRubricScores"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject
                { ["type"] = "object", ["additionalProperties"] = false, ["required"] = CreateStringArray(["criterionId", "awardedScore", "comment"]),
                  ["properties"] = new JsonObject { ["criterionId"] = new JsonObject { ["type"] = "string" },
                    ["awardedScore"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["comment"] = new JsonObject { ["type"] = "string", ["nullable"] = true } } } },
            ["answerAssessment"] = new JsonObject { ["type"] = "string", ["enum"] = CreateStringArray(["Correct", "Incorrect", "Uncertain"]) },
            ["reasoningVerdict"] = new JsonObject { ["type"] = "string", ["enum"] = CreateStringArray(["Valid", "Invalid", "Uncertain"]) },
            ["schemaVersion"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = CreateStringArray([AIAnalysisContract.SchemaVersion])
            },
            ["language"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = CreateStringArray(["vi"])
            },
            ["methodDetected"] = new JsonObject
            {
                ["type"] = "string",
                ["nullable"] = true
            },
            ["reasoningQuality"] = CreatePercentageSchema(),
            ["errorType"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = CreateStringArray(Enum.GetNames<ErrorType>())
            },
            ["misconception"] = new JsonObject
            {
                ["type"] = "string",
                ["nullable"] = true
            },
            ["missingSteps"] = CreateStringArraySchema(),
            ["rootCauseNodeIds"] = CreateStringArraySchema(),
            ["confidence"] = CreatePercentageSchema(),
            ["feedback"] = new JsonObject
            {
                ["type"] = "string"
            },
            ["solutionType"] = CreateSolutionTypeSchema(),
            ["aiSolution"] = new JsonObject
            {
                ["type"] = "string",
                ["nullable"] = true
            }
        };

        var names = PropertyNames.AsEnumerable();
        if (requireCriterionDeductions)
        {
            properties["criterionDeductions"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject {
                ["type"] = "object", ["additionalProperties"] = false,
                ["required"] = CreateStringArray(["criterionId", "evidenceKind", "unmetRequirement", "evidence"]),
                ["properties"] = new JsonObject {
                    ["criterionId"] = new JsonObject { ["type"] = "string" },
                    ["evidenceKind"] = new JsonObject { ["type"] = "string", ["enum"] = CreateStringArray(["Visual", "Nonvisual"]) },
                    ["unmetRequirement"] = new JsonObject { ["type"] = "string" },
                    ["evidence"] = new JsonObject { ["type"] = "string" } } } };
            names = names.Append("criterionDeductions");
        }
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
            ["required"] = CreateStringArray(names),
            ["propertyOrdering"] = CreateStringArray(names)
        };
    }

    public GenerateContentConfig CreateGenerateContentConfig(bool requireCriterionDeductions = false) =>
        new()
        {
            ResponseMimeType = "application/json",
            ResponseJsonSchema = CreateSchema(requireCriterionDeductions),
            CandidateCount = 1,
            Temperature = 0
        };

    public GenerateContentConfig CreateBatchConfig(IEnumerable<string> ids, bool requireCriterionDeductions = false) => new()
    {
        ResponseMimeType = "application/json", CandidateCount = 1, Temperature = 0,
        ResponseJsonSchema = new JsonObject
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["required"] = CreateStringArray(["results"]),
            ["properties"] = new JsonObject
            {
                ["results"] = new JsonObject
                {
                    ["type"] = "array", ["items"] = new JsonObject
                    {
                        ["type"] = "object", ["additionalProperties"] = false,
                        ["required"] = CreateStringArray(["itemId", "analysis"]),
                        ["properties"] = new JsonObject
                        {
                            ["itemId"] = new JsonObject { ["type"] = "string", ["enum"] = CreateStringArray(ids) },
                            ["analysis"] = CreateSchema(requireCriterionDeductions)
                        }
                    }
                }
            }
        }
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

    private static JsonObject CreateSolutionTypeSchema() =>
        new()
        {
            ["type"] = "string",
            ["nullable"] = true,
            ["enum"] = CreateStringArray(["REFINED", "CORRECTED", "GENERATED", "MODEL_ANSWER"])
        };

    private static JsonArray CreateStringArray(IEnumerable<string> values) =>
        new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
}
