using System.Collections.Generic;
using System.Text.Json;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public class RubricScoreInput
{
    public string CriterionId { get; set; } = string.Empty;
    public decimal AwardedScore { get; set; }
    public string? Comment { get; set; }
}

public class RubricCriterionGrade : RubricScoreInput
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
}

/// <summary>Immutable definition + result snapshot in the append-only review history.</summary>
public class RubricGrade
{
    public decimal MaxScore { get; set; }
    public decimal AwardedScore { get; set; }
    public List<RubricCriterionGrade> Criteria { get; set; } = new();
    public List<RubricVisualEvidence> VisualEvidence { get; set; } = new();

    public static string Serialize(RubricGrade grade) => JsonSerializer.Serialize(grade, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    public static RubricGrade? Deserialize(string? json) => string.IsNullOrWhiteSpace(json)
        ? null : JsonSerializer.Deserialize<RubricGrade>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

public sealed record RubricVisualEvidence(string CriterionId, int RequirementIndex, string Status,
    int? StudentImageIndex, string Observation)
{
    public RubricVisualEvidence() : this(string.Empty, 0, string.Empty, null, string.Empty) { }
}
