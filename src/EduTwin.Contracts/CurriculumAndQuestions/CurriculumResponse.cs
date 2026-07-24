using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CurriculumResponse
{
    public CurriculumDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
