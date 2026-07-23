using System.Collections.Generic;

namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeGraphDto
{
    public string SubjectId { get; set; } = string.Empty;
    public IReadOnlyList<KnowledgeGraphNodeDto> Nodes { get; set; } = [];
    public IReadOnlyList<KnowledgeGraphEdgeDto> Edges { get; set; } = [];
}
