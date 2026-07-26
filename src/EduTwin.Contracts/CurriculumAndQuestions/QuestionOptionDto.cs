namespace EduTwin.Contracts.CurriculumAndQuestions;

public class QuestionOptionDto
{
    public string OptionId { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string Text { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public uint OrderIndex { get; set; }
}
