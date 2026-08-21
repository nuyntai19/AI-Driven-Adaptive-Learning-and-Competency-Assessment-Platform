using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiResponseJsonSchemaTests
{
    private static readonly string[] CanonicalProperties =
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

    [Fact]
    public void TopLevel_IsObjectAndDisallowsAdditionalProperties()
    {
        using var document = CreateDocument();
        var root = document.RootElement;

        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Properties_AreExactlyTenCanonicalNames()
    {
        using var document = CreateDocument();
        var propertyNames = document.RootElement.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(CanonicalProperties, propertyNames);
    }

    [Fact]
    public void Required_ContainsExactlyAllTenProperties()
    {
        using var document = CreateDocument();

        Assert.Equal(
            CanonicalProperties,
            StringValues(document.RootElement.GetProperty("required")));
        Assert.Equal(
            CanonicalProperties,
            StringValues(document.RootElement.GetProperty("propertyOrdering")));
    }

    [Fact]
    public void SchemaVersion_UsesCanonicalSingleEnumValue()
    {
        using var document = CreateDocument();
        var schema = PropertySchema(document, "schemaVersion");

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal([AIAnalysisContract.SchemaVersion], StringValues(schema.GetProperty("enum")));
    }

    [Fact]
    public void Language_EnumIsExactlyViAndEn()
    {
        using var document = CreateDocument();
        var schema = PropertySchema(document, "language");

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(["vi", "en"], StringValues(schema.GetProperty("enum")));
    }

    [Fact]
    public void ErrorType_EnumMatchesProductionAllowList()
    {
        using var document = CreateDocument();
        var schema = PropertySchema(document, "errorType");

        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(Enum.GetNames<ErrorType>(), StringValues(schema.GetProperty("enum")));
    }

    [Fact]
    public void QualityAndConfidence_AreIntegerWithZeroToOneHundredBounds()
    {
        using var document = CreateDocument();

        foreach (var propertyName in new[] { "reasoningQuality", "confidence" })
        {
            var schema = PropertySchema(document, propertyName);
            Assert.Equal("integer", schema.GetProperty("type").GetString());
            Assert.Equal(0, schema.GetProperty("minimum").GetInt32());
            Assert.Equal(100, schema.GetProperty("maximum").GetInt32());
        }
    }

    [Fact]
    public void NullableTextFields_AllowStringOrNull()
    {
        using var document = CreateDocument();

        Assert.Equal(
            ["string", "null"],
            StringValues(PropertySchema(document, "methodDetected").GetProperty("type")));
        Assert.Equal(
            ["string", "null"],
            StringValues(PropertySchema(document, "misconception").GetProperty("type")));
    }

    [Fact]
    public void Collections_AreArraysOfStrings()
    {
        using var document = CreateDocument();

        foreach (var propertyName in new[] { "missingSteps", "rootCauseNodeIds" })
        {
            var schema = PropertySchema(document, propertyName);
            Assert.Equal("array", schema.GetProperty("type").GetString());
            Assert.Equal("string", schema.GetProperty("items").GetProperty("type").GetString());
        }
    }

    [Fact]
    public void RootCauseNodeIds_ItemsRemainString()
    {
        using var document = CreateDocument();
        var items = PropertySchema(document, "rootCauseNodeIds").GetProperty("items");

        Assert.Equal(JsonValueKind.String, items.GetProperty("type").ValueKind);
        Assert.Equal("string", items.GetProperty("type").GetString());
    }

    private static JsonDocument CreateDocument()
    {
        var schema = new GeminiResponseJsonSchema().CreateSchema();
        return JsonDocument.Parse(JsonSerializer.Serialize(schema));
    }

    private static JsonElement PropertySchema(JsonDocument document, string propertyName) =>
        document.RootElement.GetProperty("properties").GetProperty(propertyName);

    private static string[] StringValues(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetString()!).ToArray();
}
