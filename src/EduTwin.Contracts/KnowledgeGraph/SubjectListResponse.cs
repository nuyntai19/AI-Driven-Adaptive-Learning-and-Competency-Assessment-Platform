using System.Collections.Generic;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.KnowledgeGraph;

public class SubjectListResponse
{
    public IReadOnlyList<SubjectDto> Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
