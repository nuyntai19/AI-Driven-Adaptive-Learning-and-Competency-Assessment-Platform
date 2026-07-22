using System.Collections.Generic;

namespace EduTwin.BLL.KnowledgeGraph;

public class KnowledgeNodeHierarchyCycleDetector : IKnowledgeNodeHierarchyCycleDetector
{
    public bool HasCycle(ulong targetNodeId, ulong? candidateParentNodeId, IReadOnlyDictionary<ulong, ulong?> parentMap)
    {
        if (!candidateParentNodeId.HasValue)
        {
            return false;
        }

        ulong current = candidateParentNodeId.Value;

        if (current == targetNodeId)
        {
            return true;
        }

        var visited = new HashSet<ulong> { current };

        while (parentMap.TryGetValue(current, out ulong? nextParent) && nextParent.HasValue)
        {
            if (nextParent.Value == targetNodeId)
            {
                return true;
            }

            if (!visited.Add(nextParent.Value))
            {
                // Cycle in existing data detected. Fail-closed.
                return true;
            }

            current = nextParent.Value;
        }

        return false;
    }
}
