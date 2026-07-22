using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.KnowledgeGraph;

public class SubjectResponse
{
    public SubjectDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
