using System.Collections.Generic;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class GradingCriteria
{
    public string SchemaVersion { get; set; } = "1.0";
    public List<string> RequiredIdeas { get; set; } = new();
    public List<string> CommonErrors { get; set; } = new();
    public string ScoringNotes { get; set; } = string.Empty;
    public List<RubricCriterion> Criteria { get; set; } = new();
}

public class RubricCriterion
{
    public string CriterionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // Stored in the question's native units; the review UI normalizes to /10.
    public decimal MaxScore { get; set; }
}
