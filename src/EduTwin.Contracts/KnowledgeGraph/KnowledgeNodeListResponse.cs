using System.Collections.Generic;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeNodeListResponse
{
    public List<KnowledgeNodeDto> Data { get; set; } = new();
    public MetaDto Meta { get; set; } = null!;
}
