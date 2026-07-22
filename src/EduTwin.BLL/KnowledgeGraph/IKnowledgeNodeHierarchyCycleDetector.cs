using System.Collections.Generic;

namespace EduTwin.BLL.KnowledgeGraph;

public interface IKnowledgeNodeHierarchyCycleDetector
{
    bool HasCycle(ulong targetNodeId, ulong? candidateParentNodeId, IReadOnlyDictionary<ulong, ulong?> parentMap);
}
