using System.Collections.Generic;
using System.Text.Json;
using EduTwin.Contracts.CurriculumAndQuestions;
using FluentAssertions;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class GradingCriteriaJsonTests
{
    [Fact]
    public void SerializeAndDeserialize_RoundTrip_PreservesAllData()
    {
        // Arrange
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var original = new GradingCriteria
        {
            SchemaVersion = "1.5",
            RequiredIdeas = new List<string> { "First Idea", "Second Idea" },
            CommonErrors = new List<string> { "Error A" },
            ScoringNotes = "Give partial points."
        };

        // Act
        var json = JsonSerializer.Serialize(original, jsonOptions);
        var deserialized = JsonSerializer.Deserialize<GradingCriteria>(json, jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.SchemaVersion.Should().Be(original.SchemaVersion);
        deserialized.RequiredIdeas.Should().BeEquivalentTo(original.RequiredIdeas);
        deserialized.CommonErrors.Should().BeEquivalentTo(original.CommonErrors);
        deserialized.ScoringNotes.Should().Be(original.ScoringNotes);
        
        // Also verify the camelCase is used correctly in the raw json string
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"requiredIdeas\"");
        json.Should().Contain("\"commonErrors\"");
        json.Should().Contain("\"scoringNotes\"");
    }
}
