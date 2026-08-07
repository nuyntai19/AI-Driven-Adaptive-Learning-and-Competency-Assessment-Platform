using System;

namespace EduTwin.Contracts.Assignments;

public class StudentQuestionOptionDto
{
    public string OptionId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
