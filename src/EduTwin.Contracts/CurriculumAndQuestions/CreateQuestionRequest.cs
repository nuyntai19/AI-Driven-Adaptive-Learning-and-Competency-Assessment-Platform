using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CreateQuestionRequest
{
    public Guid SubjectId { get; set; }
    public string PrimaryTopicNodeId { get; set; } = null!;
    public string QuestionType { get; set; } = null!;
    public byte Difficulty { get; set; }
    public string QuestionText { get; set; } = null!;
    public string CorrectAnswer { get; set; } = null!;
    public string Solution { get; set; } = null!;
    public string? ExpectedReasoning { get; set; }
    public GradingCriteria? GradingCriteria { get; set; }
    public decimal MaxScore { get; set; } = 1m;
    public uint EstimatedTimeSeconds { get; set; }
    public bool ReasoningRequired { get; set; } = true;
    public string LanguageCode { get; set; } = "vi";
    public List<QuestionOptionInput> Options { get; set; } = new();
    public List<KnowledgeMappingInput> KnowledgeMappings { get; set; } = new();
}
