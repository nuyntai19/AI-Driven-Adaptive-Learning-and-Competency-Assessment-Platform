using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeNodeResponse
{
    public KnowledgeNodeDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
