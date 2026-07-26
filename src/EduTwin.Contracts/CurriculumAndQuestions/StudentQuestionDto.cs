using System.Collections.Generic;

namespace EduTwin.Contracts.CurriculumAndQuestions;

/// <summary>
/// Student-facing option DTO — no IsCorrect field.
/// </summary>
public class StudentQuestionOptionDto
{
    public string OptionId { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Text { get; set; } = null!;
    public uint OrderIndex { get; set; }
}

/// <summary>
/// Student-facing Question DTO — excludes correctAnswer, solution, expectedReasoning, gradingCriteria, and isCorrect on options.
/// </summary>
public class StudentQuestionDto
{
    public string QuestionId { get; set; } = null!;
    public string SubjectId { get; set; } = null!;
    public string PrimaryTopicNodeId { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public byte Difficulty { get; set; }
    public string QuestionText { get; set; } = null!;
    public decimal MaxScore { get; set; }
    public uint EstimatedTimeSeconds { get; set; }
    public bool ReasoningRequired { get; set; }
    public string LanguageCode { get; set; } = null!;
    public List<StudentQuestionOptionDto> Options { get; set; } = new();
}
