using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations;

public sealed class CandidateBuildResult
{
    public bool IsAllMastered { get; init; }
    public string? BlockedReason { get; init; }
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
    Task<CandidateBuildResult> BuildForClassAsync(Guid center, Guid student, Guid subject, Guid classId, CancellationToken ct)
        => BuildCandidatesAsync(center, student, subject, ct);
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
        => await BuildCoreAsync(centerId, studentId, subjectId, null, cancellationToken);

    public Task<CandidateBuildResult> BuildForClassAsync(Guid center, Guid student, Guid subject, Guid classId, CancellationToken ct)
        => BuildCoreAsync(center, student, subject, classId, ct);

    private async Task<CandidateBuildResult> BuildCoreAsync(Guid centerId, Guid studentId, Guid subjectId, Guid? classId, CancellationToken cancellationToken)
    {
        var scope = await EduTwin.BLL.Organization.StudentLearningScope.ResolveAsync(
            _dbContext, centerId, studentId, subjectId, classId, false, cancellationToken);
        if (!scope.Allowed)
        {
            var memberships = await EduTwin.BLL.Organization.StudentClassScope.Memberships(_dbContext, centerId, studentId)
                .Where(m => m.SubjectId == subjectId).ToListAsync(cancellationToken);
            var hasCurrent = memberships.Any(m => !m.IsHistorical && (!classId.HasValue || m.ClassId == classId));
            return new CandidateBuildResult { BlockedReason = hasCurrent ? "NO_APPLIED_CURRICULUM"
                : classId.HasValue || memberships.Count > 0 ? "NO_ACTIVE_CLASS" : "NO_APPROVED_GRAPH" };
        }
        var scopedIds = scope.TopicIds!.ToList();
        var allActiveTopicNodes = await _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == subjectId && n.NodeType == NodeType.Topic &&
                n.IsActive && !n.IsDeleted && scopedIds.Contains(n.NodeId))
            .OrderBy(n => n.OrderIndex).ThenBy(n => n.NodeId).ToListAsync(cancellationToken);
        foreach (var node in allActiveTopicNodes)
            node.OrderIndex = scope.OrderByNodeId.GetValueOrDefault(node.NodeId, node.OrderIndex);
        allActiveTopicNodes = allActiveTopicNodes.OrderBy(n => n.OrderIndex).ThenBy(n => n.NodeId).ToList();
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

        // 3. Query prerequisite edges in this subject
        var topology = await EduTwin.BLL.Organization.ScopedKnowledgeTopology.ReadAsync(
            _dbContext, centerId, subjectId, scope, cancellationToken);
        var edges = topology.Edges.Where(e => e.RelationType == RelationType.PrerequisiteOf).ToList();
        var prerequisitesByTarget = edges
            .GroupBy(e => e.TargetNodeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SourceNodeId).Distinct().ToList());

        // Candidates remain curriculum-scoped, but prerequisite readiness is subject-scoped.
        // A valid prerequisite outside the curriculum must use its real mastery instead of 0.
        var prerequisiteNodeIds = prerequisitesByTarget.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToArray();
        var validPrerequisiteNodeIds = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.CenterId == centerId
                && n.SubjectId == subjectId
                && n.NodeType == NodeType.Topic
                && n.IsActive
                && !n.IsDeleted
                && prerequisiteNodeIds.Contains(n.NodeId))
            .Select(n => n.NodeId)
            .ToListAsync(cancellationToken);

        var twinsByNodeId = twins.ToDictionary(t => t.TopicNodeId, t => t.MasteryPercentage);
        var masteryByNodeId = allActiveTopicNodes.ToDictionary(
            n => n.NodeId,
            n => twinsByNodeId.GetValueOrDefault(n.NodeId, 0m));
        foreach (var prerequisiteNodeId in validPrerequisiteNodeIds)
        {
            masteryByNodeId.TryAdd(
                prerequisiteNodeId,
                twinsByNodeId.GetValueOrDefault(prerequisiteNodeId, 0m));
        }

        // 4. Query all non-superseded (head) evidence assessments for this student and subject
        // Relational fix: Include Attempt.Question so PrimaryTopicNodeId is available after materialization
        var allAssessments = await _dbContext.EvidenceAssessments
            .Include(e => e.Attempt)
                .ThenInclude(a => a.Question)
            .Include(e => e.Analysis)
            .Where(e => e.CenterId == centerId
                && e.Attempt.StudentId == studentId
                && e.Attempt.Question.SubjectId == subjectId
                && scopedIds.Contains(e.Attempt.Question.PrimaryTopicNodeId))
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

            var (topicWeightedAvg, topicSampleCount, topicWeightSum) =
                EffectiveEvidenceResolver.CalculateWeightedReasoningAverage(topicHeads);

            decimal? effectiveReasoningAvg = topicSampleCount > 0
                ? topicWeightedAvg
                : subjectWeightedAvg;

            int sampleCount = topicSampleCount > 0 ? topicSampleCount : subjectSampleCount;
            decimal weightSum = topicSampleCount > 0 ? topicWeightSum : subjectWeightSum;
            string qualitySource = topicSampleCount > 0 ? "TopicWindow" : "SubjectFallback";

            eligibleInputs.Add(new TopicCandidateEvaluationInput(
                TopicNodeId: node.NodeId,
                TopicName: node.NodeName,
                OrderIndex: node.OrderIndex,
                ExamImportance: node.ExamImportance,
                EstimatedLearningMinutes: node.EstimatedLearningMinutes,
                CurrentMastery: currentMastery,
                PrerequisiteMasteries: prereqMasteries,
                WeightedRecentReasoningAverage: effectiveReasoningAvg,
                ReasoningQualitySampleCount: sampleCount,
                ReasoningWeightSum: weightSum,
                ReasoningQualitySource: qualitySource));
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
