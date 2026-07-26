using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class QuestionResponse
{
    public QuestionDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
