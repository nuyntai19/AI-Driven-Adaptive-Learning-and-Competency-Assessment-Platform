using System.Collections.Generic;

namespace EduTwin.Contracts.CurriculumAndQuestions;

/// <summary>
/// Teacher-facing Question DTO — includes correctAnswer, solution, expectedReasoning, gradingCriteria and isCorrect on options.
/// </summary>
public class QuestionDto
{
    public string QuestionId { get; set; } = null!;
    public string SubjectId { get; set; } = null!;
    public string PrimaryTopicNodeId { get; set; } = null!;
    public string CreatedByTeacherId { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public byte Difficulty { get; set; }
    public string QuestionText { get; set; } = null!;
    public string CorrectAnswer { get; set; } = null!;
    public string Solution { get; set; } = null!;
    public string? ExpectedReasoning { get; set; }
    public GradingCriteria? GradingCriteria { get; set; }
    public decimal MaxScore { get; set; }
    public uint EstimatedTimeSeconds { get; set; }
    public bool ReasoningRequired { get; set; }
    public string LanguageCode { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string AnswerEvaluationMode { get; set; } = null!;
    public List<QuestionOptionDto> Options { get; set; } = new();
    public List<KnowledgeMappingDto> KnowledgeMappings { get; set; } = new();
    public string RowVersion { get; set; } = null!;
}
