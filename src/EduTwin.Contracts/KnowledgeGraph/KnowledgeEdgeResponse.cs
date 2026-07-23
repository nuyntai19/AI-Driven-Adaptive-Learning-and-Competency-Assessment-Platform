using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeEdgeResponse
{
    public KnowledgeEdgeDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
