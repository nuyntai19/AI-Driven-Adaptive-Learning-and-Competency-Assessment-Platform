using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Assignments;

public class StudentQuestionDto
{
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public int Difficulty { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int EstimatedTimeSeconds { get; set; }
    public bool ReasoningRequired { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public List<StudentQuestionOptionDto> Options { get; set; } = new();
    public string? AttemptStatus { get; set; }
}
