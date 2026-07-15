using System.Collections.Generic;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class GradingCriteria
{
    public string SchemaVersion { get; set; } = "1.0";
    public List<string> RequiredIdeas { get; set; } = new();
    public List<string> CommonErrors { get; set; } = new();
    public string ScoringNotes { get; set; } = string.Empty;
}
