namespace EduTwin.Contracts.Assignments;

public class AssignmentQuestionDto
{
    public string QuestionId { get; set; } = string.Empty;
    public uint OrderIndex { get; set; }
    public decimal Points { get; set; }
}
