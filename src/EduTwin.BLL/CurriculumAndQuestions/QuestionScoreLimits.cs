namespace EduTwin.BLL.CurriculumAndQuestions;

/// <summary>Shared by manual create/update and import; matches MySQL decimal(5,2).</summary>
public static class QuestionScoreLimits
{
    public static bool IsValid(decimal score) => score > 0 && score <= 999.99m && decimal.Round(score, 2) == score;
}
