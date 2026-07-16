using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class KnowledgeGraphValidator
{
    public void ValidateDag(IEnumerable<KnowledgeEdge> edges)
    {
        var relevantEdges = edges
            .Where(e => e.RelationType == EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf || e.RelationType == EduTwin.Contracts.KnowledgeGraph.RelationType.PartOf)
            .ToList();

        if (!relevantEdges.Any())
            return;

        // Check self loops
        if (relevantEdges.Any(e => e.SourceNodeId == e.TargetNodeId))
        {
            throw new InvalidOperationException("DAG Validation failed: Self-loop detected.");
        }

        // Check cycles using topological sort (DFS)
        var graph = new Dictionary<ulong, List<ulong>>();
        foreach (var edge in relevantEdges)
        {
            if (!graph.ContainsKey(edge.SourceNodeId))
                graph[edge.SourceNodeId] = new List<ulong>();
            if (!graph.ContainsKey(edge.TargetNodeId))
                graph[edge.TargetNodeId] = new List<ulong>();
                
            graph[edge.SourceNodeId].Add(edge.TargetNodeId);
        }

        var visited = new HashSet<ulong>();
        var recStack = new HashSet<ulong>();

        foreach (var node in graph.Keys)
        {
            if (!visited.Contains(node))
            {
                if (IsCyclic(node, graph, visited, recStack))
                {
                    throw new InvalidOperationException("DAG Validation failed: Cycle detected.");
                }
            }
        }
    }

    private bool IsCyclic(ulong node, Dictionary<ulong, List<ulong>> graph, HashSet<ulong> visited, HashSet<ulong> recStack)
    {
        if (recStack.Contains(node))
            return true;

        if (visited.Contains(node))
            return false;

        visited.Add(node);
        recStack.Add(node);

        if (graph.ContainsKey(node))
        {
            foreach (var neighbor in graph[node])
            {
                if (IsCyclic(neighbor, graph, visited, recStack))
                    return true;
            }
        }

        recStack.Remove(node);
        return false;
    }
}
