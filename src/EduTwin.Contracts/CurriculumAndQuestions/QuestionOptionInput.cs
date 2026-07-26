namespace EduTwin.Contracts.CurriculumAndQuestions;

public class QuestionOptionInput
{
    public string OptionLabel { get; set; } = null!;
    public string OptionText { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public uint OrderIndex { get; set; }
}
