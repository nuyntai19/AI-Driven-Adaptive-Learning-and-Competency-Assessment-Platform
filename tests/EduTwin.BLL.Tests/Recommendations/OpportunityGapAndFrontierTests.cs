using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.BLL.Recommendations;
using EduTwin.DAL.KnowledgeGraph;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class OpportunityGapAndFrontierTests
{
    [Fact]
    public void OpportunityGapCalculator_PreservesExactFrozenFormulaAndCoefficients()
    {
        // Setup candidate with exact known parameters:
        // Mastery: 40% -> Mastery01 = 0.40
        // ExamImportance: 80
        // ExpectedScoreGain: (1 - 0.40) * 80 = 48.00
        // ReasoningAverage: 70% -> RecentReasoningAverage01 = 0.70
        // PrerequisiteReadiness: 90% -> 0.90
        // ProbabilityOfMastery = 0.20 + (0.60 * 0.70) + (0.20 * 0.90) = 0.20 + 0.42 + 0.18 = 0.80
        // EstimatedMinutes: 90 min = 1.5 hours -> LearningEffort = 1.50
        // RawOpportunity = (48.00 * 0.80) / 1.50 = 38.40 / 1.50 = 25.60
        var input = new TopicCandidateEvaluationInput(
            TopicNodeId: 101,
            TopicName: "Topic Alpha",
            OrderIndex: 1,
            ExamImportance: 80m,
            EstimatedLearningMinutes: 90,
            CurrentMastery: 40m,
            PrerequisiteMasteries: new List<decimal> { 90m },
            WeightedRecentReasoningAverage: 70m);

        var results = OpportunityGapCalculator.Evaluate(new[] { input });

        Assert.Single(results);
        var scored = results[0];

        Assert.Equal(48.00m, scored.ExpectedScoreGain);
        Assert.Equal(0.80m, scored.ProbabilityOfMastery);
        Assert.Equal(1.50m, scored.EstimatedLearningHours);
        Assert.Equal(25.60m, scored.RawOpportunity);
        Assert.Equal(100.00m, scored.NormalizedOpportunityScore); // Single candidate normalizes to 100
        Assert.Equal(1, scored.Rank);
    }

    [Fact]
    public void OpportunityGapCalculator_EnforcesMinimumEffortOfHalfHour()
    {
        // 10 minutes -> 0.1667h, clamped to min 0.5h effort
        var input = new TopicCandidateEvaluationInput(
            TopicNodeId: 102,
            TopicName: "Topic Quick",
            OrderIndex: 1,
            ExamImportance: 100m,
            EstimatedLearningMinutes: 10,
            CurrentMastery: 0m,
            PrerequisiteMasteries: Array.Empty<decimal>(),
            WeightedRecentReasoningAverage: 100m);

        var results = OpportunityGapCalculator.Evaluate(new[] { input });
        var scored = results[0];

        // ExpectedScoreGain = (1 - 0) * 100 = 100
        // ProbabilityOfMastery = Clamp(0.20 + 0.60 + 0.20) = 1.00
        // LearningEffort = Max(10/60, 0.5) = 0.5
        // Raw = (100 * 1.0) / 0.5 = 200.00
        Assert.Equal(100.00m, scored.ExpectedScoreGain);
        Assert.Equal(1.00m, scored.ProbabilityOfMastery);
        Assert.Equal(200.00m, scored.RawOpportunity);
    }

    [Fact]
    public void ReadyPrerequisiteFrontier_Chain_SelectsOnlyFrontierNode()
    {
        // A -> B -> C (A=20, B=0, C=0)
        // B depends on A (A is prerequisite of B)
        // C depends on B (B is prerequisite of C)
        var nodeA = new KnowledgeNode { NodeId = 1, NodeName = "A", OrderIndex = 1 };
        var nodeB = new KnowledgeNode { NodeId = 2, NodeName = "B", OrderIndex = 2 };
        var nodeC = new KnowledgeNode { NodeId = 3, NodeName = "C", OrderIndex = 3 };

        var allNodes = new List<KnowledgeNode> { nodeA, nodeB, nodeC };

        var masteryByNode = new Dictionary<ulong, decimal>
        {
            [1] = 20m,
            [2] = 0m,
            [3] = 0m
        };

        var prereqsByTarget = new Dictionary<ulong, List<ulong>>
        {
            [2] = new List<ulong> { 1 }, // B requires A
            [3] = new List<ulong> { 2 }  // C requires B
        };

        var selector = new LinearFallbackSelector();
        var result = selector.SelectTopics(allNodes, masteryByNode, prereqsByTarget);

        Assert.False(result.IsBlocked);
        Assert.Single(result.SelectedTopics);
        Assert.Equal(1ul, result.SelectedTopics[0].NodeId); // Only A is ready on the frontier!
    }

    [Fact]
    public void ReadyPrerequisiteFrontier_CyclicDeadlock_ReturnsBlockedDiagnostic()
    {
        // A depends on B, B depends on A, both at 0%
        var nodeA = new KnowledgeNode { NodeId = 1, NodeName = "A", OrderIndex = 1 };
        var nodeB = new KnowledgeNode { NodeId = 2, NodeName = "B", OrderIndex = 2 };

        var allNodes = new List<KnowledgeNode> { nodeA, nodeB };

        var masteryByNode = new Dictionary<ulong, decimal>
        {
            [1] = 0m,
            [2] = 0m
        };

        var prereqsByTarget = new Dictionary<ulong, List<ulong>>
        {
            [1] = new List<ulong> { 2 },
            [2] = new List<ulong> { 1 }
        };

        var selector = new LinearFallbackSelector();
        var result = selector.SelectTopics(allNodes, masteryByNode, prereqsByTarget);

        Assert.True(result.IsBlocked);
        Assert.Equal(LinearFallbackResult.PrerequisiteGraphBlocked, result.DiagnosticCode);
        Assert.Empty(result.SelectedTopics);
    }

    [Fact]
    public void MaintenanceReview_TieBreak_OrdersCorrectly()
    {
        // When all topics >= 80%, tie break order:
        // Mastery ASC -> ExamImportance DESC -> OrderIndex ASC -> TopicId ASC
        var node1 = new KnowledgeNode { NodeId = 1, NodeName = "T1", OrderIndex = 1, ExamImportance = 50m };
        var node2 = new KnowledgeNode { NodeId = 2, NodeName = "T2", OrderIndex = 2, ExamImportance = 90m };
        var node3 = new KnowledgeNode { NodeId = 3, NodeName = "T3", OrderIndex = 3, ExamImportance = 50m };
        var node4 = new KnowledgeNode { NodeId = 4, NodeName = "T4", OrderIndex = 4, ExamImportance = 50m };

        var masteries = new Dictionary<ulong, decimal>
        {
            [1] = 85m,
            [2] = 85m, // Same mastery as T1, but higher ExamImportance (90 > 50) -> T2 wins over T1
            [3] = 80m, // Lower mastery (80 < 85) -> T3 wins overall!
            [4] = 95m
        };

        var ordered = new[] { node1, node2, node3, node4 }
            .OrderBy(n => masteries[n.NodeId])
            .ThenByDescending(n => n.ExamImportance)
            .ThenBy(n => n.OrderIndex)
            .ThenBy(n => n.NodeId)
            .ToList();

        Assert.Equal(3ul, ordered[0].NodeId); // 80% lowest mastery
        Assert.Equal(2ul, ordered[1].NodeId); // 85% mastery with 90 ExamImportance
        Assert.Equal(1ul, ordered[2].NodeId); // 85% mastery with 50 ExamImportance
        Assert.Equal(4ul, ordered[3].NodeId); // 95% mastery
    }
}
