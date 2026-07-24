using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeGraphResponse
{
    public KnowledgeGraphDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
