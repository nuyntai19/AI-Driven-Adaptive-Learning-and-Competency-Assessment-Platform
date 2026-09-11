using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations;

public sealed class CandidateBuildResult
{
    public bool IsAllMastered { get; init; }
    public IReadOnlyList<TopicCandidateEvaluationInput> EligibleCandidates { get; init; } = Array.Empty<TopicCandidateEvaluationInput>();
    public IReadOnlyList<KnowledgeNode> AllActiveTopicNodes { get; init; } = Array.Empty<KnowledgeNode>();
    public IReadOnlyDictionary<ulong, decimal> MasteryByTopicNodeId { get; init; } = new Dictionary<ulong, decimal>();
    public IReadOnlyDictionary<ulong, List<ulong>> PrerequisitesByTopicNodeId { get; init; } = new Dictionary<ulong, List<ulong>>();
    public decimal? SubjectWeightedReasoningAverage { get; init; }
    public int SubjectReasoningSampleCount { get; init; }
    public decimal SubjectReasoningWeightSum { get; init; }
    public int EffectiveEvidenceCount { get; init; }
}

public interface IOpportunityCandidateBuilder
{
    Task<CandidateBuildResult> BuildCandidatesAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        CancellationToken cancellationToken);
}

public sealed class OpportunityCandidateBuilder : IOpportunityCandidateBuilder
{
    private readonly EduTwinDbContext _dbContext;

    public OpportunityCandidateBuilder(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CandidateBuildResult> BuildCandidatesAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        CancellationToken cancellationToken)
    {
        // 1. Query all active topic nodes in subject
        var allActiveTopicNodes = await _dbContext.KnowledgeNodes
            .Where(n => n.CenterId == centerId
                && n.SubjectId == subjectId
                && n.NodeType == NodeType.Topic
                && n.IsActive
                && !n.IsDeleted)
            .OrderBy(n => n.OrderIndex)
            .ThenBy(n => n.NodeId)
            .ToListAsync(cancellationToken);

        if (allActiveTopicNodes.Count == 0)
        {
            return new CandidateBuildResult();
        }

        // 2. Query student's knowledge twins for this subject (LEFT JOIN semantics: missing twin => mastery = 0)
        var twins = await _dbContext.KnowledgeTwins
            .Where(kt => kt.CenterId == centerId
                && kt.StudentId == studentId
                && kt.SubjectId == subjectId
                && !kt.IsDeleted)
            .ToListAsync(cancellationToken);

        var masteryByNodeId = allActiveTopicNodes.ToDictionary(
            n => n.NodeId,
            n => twins.FirstOrDefault(t => t.TopicNodeId == n.NodeId)?.MasteryPercentage ?? 0m);

        // 3. Query prerequisite edges in this subject
        var edges = await _dbContext.KnowledgeEdges
            .Where(e => e.CenterId == centerId
                && e.SubjectId == subjectId
                && e.RelationType == RelationType.PrerequisiteOf
                && !e.IsDeleted)
            .ToListAsync(cancellationToken);

        var prerequisitesByTarget = edges
            .GroupBy(e => e.TargetNodeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SourceNodeId).Distinct().ToList());

        // 4. Query all non-superseded (head) evidence assessments for this student and subject
        var allAssessments = await _dbContext.EvidenceAssessments
            .Include(e => e.Attempt)
            .Include(e => e.Analysis)
            .Where(e => e.CenterId == centerId
                && e.Attempt.StudentId == studentId
                && e.Attempt.Question.SubjectId == subjectId)
            .OrderByDescending(e => e.EvaluatedAt)
            .ThenByDescending(e => e.EvidenceAssessmentId)
            .ToListAsync(cancellationToken);

        var effectiveHeads = EffectiveEvidenceResolver.GetEffectiveHeads(allAssessments);
        int effectiveEvidenceCount = EffectiveEvidenceResolver.CountEffectiveGovernedEvidence(allAssessments);
        var (subjectWeightedAvg, subjectSampleCount, subjectWeightSum) =
            EffectiveEvidenceResolver.CalculateWeightedReasoningAverage(effectiveHeads.Take(5));

        // 5. Check if all active topics have mastery >= 80% (MaintenanceReview mode)
        bool isAllMastered = allActiveTopicNodes.All(n => masteryByNodeId[n.NodeId] >= 80.00m);
        if (isAllMastered)
        {
            return new CandidateBuildResult
            {
                IsAllMastered = true,
                AllActiveTopicNodes = allActiveTopicNodes,
                MasteryByTopicNodeId = masteryByNodeId,
                PrerequisitesByTopicNodeId = prerequisitesByTarget,
                SubjectWeightedReasoningAverage = subjectWeightedAvg,
                SubjectReasoningSampleCount = subjectSampleCount,
                SubjectReasoningWeightSum = subjectWeightSum,
                EffectiveEvidenceCount = effectiveEvidenceCount
            };
        }

        // 6. Filter eligible candidates: Mastery < 80% AND all prerequisites >= 60%
        var eligibleInputs = new List<TopicCandidateEvaluationInput>();

        foreach (var node in allActiveTopicNodes)
        {
            decimal currentMastery = masteryByNodeId[node.NodeId];
            if (currentMastery >= 80.00m)
            {
                continue; // already mastered
            }

            var prereqIds = prerequisitesByTarget.GetValueOrDefault(node.NodeId) ?? new List<ulong>();
            var prereqMasteries = prereqIds.Select(id => masteryByNodeId.GetValueOrDefault(id, 0m)).ToList();

            // Prerequisite Gating: Every prerequisite topic must have mastery >= 60.00%
            if (prereqMasteries.Any(m => m < 60.00m))
            {
                continue; // locked
            }

            // Topic-specific recent positive-weight reasoning quality (up to 3 samples)
            var topicHeads = effectiveHeads
                .Where(h => h.Attempt.Question.PrimaryTopicNodeId == node.NodeId)
                .Take(3)
                .ToList();

            var (topicWeightedAvg, topicSampleCount, _) =
                EffectiveEvidenceResolver.CalculateWeightedReasoningAverage(topicHeads);

            decimal? effectiveReasoningAvg = topicSampleCount > 0
                ? topicWeightedAvg
                : subjectWeightedAvg;

            eligibleInputs.Add(new TopicCandidateEvaluationInput(
                TopicNodeId: node.NodeId,
                TopicName: node.NodeName,
                OrderIndex: node.OrderIndex,
                ExamImportance: node.ExamImportance,
                EstimatedLearningMinutes: node.EstimatedLearningMinutes,
                CurrentMastery: currentMastery,
                PrerequisiteMasteries: prereqMasteries,
                WeightedRecentReasoningAverage: effectiveReasoningAvg));
        }

        return new CandidateBuildResult
        {
            IsAllMastered = false,
            EligibleCandidates = eligibleInputs,
            AllActiveTopicNodes = allActiveTopicNodes,
            MasteryByTopicNodeId = masteryByNodeId,
            PrerequisitesByTopicNodeId = prerequisitesByTarget,
            SubjectWeightedReasoningAverage = subjectWeightedAvg,
            SubjectReasoningSampleCount = subjectSampleCount,
            SubjectReasoningWeightSum = subjectWeightSum,
            EffectiveEvidenceCount = effectiveEvidenceCount
        };
    }
}
