using System.Collections.Generic;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CurriculumListResponse
{
    public List<CurriculumDto> Data { get; set; } = new();
    public MetaDto Meta { get; set; } = null!;
}
