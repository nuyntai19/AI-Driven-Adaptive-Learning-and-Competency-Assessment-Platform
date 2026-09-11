using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.BLL.Recommendations;

public sealed class LinearFallbackResult
{
    public const string PrerequisiteGraphBlocked = "PREREQUISITE_GRAPH_BLOCKED";

    public bool IsBlocked { get; init; }
    public string? DiagnosticCode { get; init; }
    public IReadOnlyList<KnowledgeNode> SelectedTopics { get; init; } = Array.Empty<KnowledgeNode>();
}

public interface ILinearFallbackSelector
{
    LinearFallbackResult SelectTopics(
        IReadOnlyList<KnowledgeNode> allActiveTopicNodes,
        IReadOnlyDictionary<ulong, decimal> masteryByTopicNodeId,
        IReadOnlyDictionary<ulong, List<ulong>> prerequisitesByTopicNodeId);
}

public sealed class LinearFallbackSelector : ILinearFallbackSelector
{
    public LinearFallbackResult SelectTopics(
        IReadOnlyList<KnowledgeNode> allActiveTopicNodes,
        IReadOnlyDictionary<ulong, decimal> masteryByTopicNodeId,
        IReadOnlyDictionary<ulong, List<ulong>> prerequisitesByTopicNodeId)
    {
        ArgumentNullException.ThrowIfNull(allActiveTopicNodes);
        ArgumentNullException.ThrowIfNull(masteryByTopicNodeId);
        ArgumentNullException.ThrowIfNull(prerequisitesByTopicNodeId);

        var unmastered = allActiveTopicNodes
            .Where(n => masteryByTopicNodeId.GetValueOrDefault(n.NodeId, 0m) < 80.00m)
            .ToList();

        if (unmastered.Count == 0)
        {
            return new LinearFallbackResult
            {
                IsBlocked = false,
                SelectedTopics = Array.Empty<KnowledgeNode>()
            };
        }

        // Ready Prerequisite Frontier:
        // An unmastered topic is ready if all its prerequisites in this subject have Mastery >= 60.00%
        var readyFrontier = new List<KnowledgeNode>();

        foreach (var node in unmastered)
        {
            var prereqIds = prerequisitesByTopicNodeId.GetValueOrDefault(node.NodeId) ?? new List<ulong>();
            bool allPrereqsMet = prereqIds.All(pId => masteryByTopicNodeId.GetValueOrDefault(pId, 0m) >= 60.00m);

            if (allPrereqsMet)
            {
                readyFrontier.Add(node);
            }
        }

        if (readyFrontier.Count == 0)
        {
            return new LinearFallbackResult
            {
                IsBlocked = true,
                DiagnosticCode = LinearFallbackResult.PrerequisiteGraphBlocked,
                SelectedTopics = Array.Empty<KnowledgeNode>()
            };
        }

        // Order by syllabus: OrderIndex ASC, then NodeId ASC, take up to 5
        var ordered = readyFrontier
            .OrderBy(n => n.OrderIndex)
            .ThenBy(n => n.NodeId)
            .Take(5)
            .ToList();

        return new LinearFallbackResult
        {
            IsBlocked = false,
            SelectedTopics = ordered
        };
    }
}
