using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Recommendations;

public sealed class RecommendationEngine : IRecommendationEngine
{
    private static ulong GenerateUlongId()
    {
        ulong id;
        do
        {
            byte[] bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            id = BitConverter.ToUInt64(bytes, 0);
        } while (id == 0);

        return id;
    }

    private readonly EduTwinDbContext _dbContext;
    private readonly IOpportunityCandidateBuilder _candidateBuilder;
    private readonly ILinearFallbackSelector _linearFallbackSelector;
    private readonly IAdaptiveQuestionSelector _questionSelector;

    public RecommendationEngine(
        EduTwinDbContext dbContext,
        IOpportunityCandidateBuilder candidateBuilder,
        ILinearFallbackSelector linearFallbackSelector,
        IAdaptiveQuestionSelector questionSelector)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _candidateBuilder = candidateBuilder ?? throw new ArgumentNullException(nameof(candidateBuilder));
        _linearFallbackSelector = linearFallbackSelector ?? throw new ArgumentNullException(nameof(linearFallbackSelector));
        _questionSelector = questionSelector ?? throw new ArgumentNullException(nameof(questionSelector));
    }

    public async Task<RecommendationGenerationResult> GenerateAndPersistAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        ulong? sourceAttemptId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // 0. Transaction B Isolation: Assert no uncommitted transaction and clear change tracker
        Debug.Assert(_dbContext.Database.CurrentTransaction is null, "Recommendation Transaction B requires a clean context without an active transaction.");
        if (_dbContext.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException("Pending changes detected before recommendation Transaction B.");
        }
        _dbContext.ChangeTracker.Clear();

        await using var transactionB = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 1. Acquire tenant-safe student row lock under Transaction B
        await StudentLockHelper.AcquireStudentLockAsync(_dbContext, centerId, studentId, cancellationToken);

        // 2. Stale-Trigger Protection: Check if a newer recommendation has already been generated
        var latestActiveRec = await _dbContext.Recommendations
            .Where(r => r.CenterId == centerId
                && r.StudentId == studentId
                && r.SubjectId == subjectId
                && r.Status == RecommendationStatus.Active
                && !r.IsDeleted)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestActiveRec is not null)
        {
            if (latestActiveRec.GeneratedAt > utcNow ||
                (latestActiveRec.GeneratedAt == utcNow && latestActiveRec.SourceAttemptId.HasValue && sourceAttemptId.HasValue && latestActiveRec.SourceAttemptId.Value > sourceAttemptId.Value))
            {
                await transactionB.RollbackAsync(cancellationToken);
                return RecommendationGenerationResult.StaleIgnored("A newer recommendation exists.");
            }
        }

        // 3. Build candidate inputs
        var candidateResult = await _candidateBuilder.BuildCandidatesAsync(
            centerId,
            studentId,
            subjectId,
            cancellationToken);

        if (candidateResult.BlockedReason is not null)
        {
            // Fail closed on ambiguous curriculum assignment: supersede active artifacts and commit
            await SupersedeActiveArtifactsAsync(centerId, studentId, subjectId, utcNow, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transactionB.CommitAsync(cancellationToken);
            return RecommendationGenerationResult.Blocked(candidateResult.BlockedReason);
        }

        if (candidateResult.AllActiveTopicNodes.Count == 0)
        {
            // No active topics: supersede active artifacts and commit
            await SupersedeActiveArtifactsAsync(centerId, studentId, subjectId, utcNow, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transactionB.CommitAsync(cancellationToken);
            return RecommendationGenerationResult.NoCandidate("No active topic nodes found.");
        }

        // 4. Determine Strategy & Sequence
        LearningPathStrategy strategy;
        RecommendationType recType;
        ulong topTopicNodeId;
        string topTopicName;
        decimal? topOpportunityScore = null;
        string calculationVersion;
        JsonDocument calculationBreakdown;
        string explanation;
        decimal topMastery;
        Question? topQuestion = null;
        var pathItems = new List<LearningPathItem>();

        int effectiveEvidenceCount = candidateResult.EffectiveEvidenceCount;

        if (candidateResult.IsAllMastered)
        {
            // Mode A: MaintenanceReview (All topics >= 80%)
            strategy = LearningPathStrategy.MaintenanceReview;
            recType = RecommendationType.TopicAndQuestion;

            var maintenanceTopics = candidateResult.AllActiveTopicNodes
                .OrderBy(n => candidateResult.MasteryByTopicNodeId.GetValueOrDefault(n.NodeId, 0m))
                .ThenByDescending(n => n.ExamImportance)
                .ThenBy(n => n.OrderIndex)
                .ThenBy(n => n.NodeId)
                .Take(5)
                .ToList();

            var topNode = maintenanceTopics[0];
            topTopicNodeId = topNode.NodeId;
            topTopicName = topNode.NodeName;
            topMastery = candidateResult.MasteryByTopicNodeId.GetValueOrDefault(topTopicNodeId, 0m);
            calculationVersion = "maintenance-v1";
            explanation = RecommendationExplanationBuilder.BuildMaintenanceReviewExplanation(topTopicName, topMastery);

            calculationBreakdown = JsonSerializer.SerializeToDocument(new
            {
                CalculationVersion = calculationVersion,
                Strategy = strategy.ToString(),
                TopTopicNodeId = topTopicNodeId,
                TopTopicName = topTopicName,
                CurrentMastery = topMastery,
                ExamImportance = topNode.ExamImportance,
                TotalTopicsCount = candidateResult.AllActiveTopicNodes.Count,
                EffectiveEvidenceCount = effectiveEvidenceCount
            });

            for (int i = 0; i < maintenanceTopics.Count; i++)
            {
                var node = maintenanceTopics[i];
                var m = candidateResult.MasteryByTopicNodeId.GetValueOrDefault(node.NodeId, 0m);
                var itemReason = RecommendationExplanationBuilder.BuildMaintenanceReviewExplanation(node.NodeName, m);
                var itemQuestion = await _questionSelector.SelectQuestionAsync(
                    centerId,
                    studentId,
                    node.NodeId,
                    m,
                    cancellationToken);

                if (i == 0)
                {
                    topQuestion = itemQuestion;
                }

                var itemBreakdown = JsonSerializer.SerializeToDocument(new
                {
                    CalculationVersion = calculationVersion,
                    Strategy = strategy.ToString(),
                    TopicNodeId = node.NodeId,
                    TopicName = node.NodeName,
                    CurrentMastery = m,
                    ExamImportance = node.ExamImportance,
                    TotalTopicsCount = candidateResult.AllActiveTopicNodes.Count,
                    EffectiveEvidenceCount = effectiveEvidenceCount
                });

                pathItems.Add(new LearningPathItem
                {
                    LearningPathItemId = GenerateUlongId(),
                    CenterId = centerId,
                    TopicNodeId = node.NodeId,
                    RecommendedQuestionId = itemQuestion?.QuestionId,
                    RankOrder = (uint)(i + 1),
                    OpportunityScore = null,
                    CalculationBreakdown = itemBreakdown,
                    Reason = itemReason,
                    Status = i == 0 ? LearningPathItemStatus.Current : LearningPathItemStatus.Pending,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }
        }
        else if (effectiveEvidenceCount >= 3
            && candidateResult.EligibleCandidates.Count > 0
            && candidateResult.SubjectReasoningSampleCount > 0)
        {
            // Mode B: OpportunityGap (>= 3 effective evidence, eligible candidates present, reasoning samples available)
            strategy = LearningPathStrategy.OpportunityGap;
            recType = RecommendationType.TopicAndQuestion;
            calculationVersion = OpportunityGapCalculator.Version;

            var ranked = OpportunityGapCalculator.Evaluate(candidateResult.EligibleCandidates);
            var topCandidates = ranked.Take(5).ToList();

            var topScored = topCandidates[0];
            topTopicNodeId = topScored.TopicNodeId;
            topTopicName = topScored.TopicName;
            topOpportunityScore = topScored.NormalizedOpportunityScore;
            topMastery = topScored.CurrentMastery;
            explanation = RecommendationExplanationBuilder.BuildOpportunityGapExplanation(
                topTopicName,
                topOpportunityScore.Value,
                topMastery,
                topScored.ExamImportance,
                topScored.PrerequisiteReadiness);

            var topBreakdownDto = new OpportunityGapBreakdown(
                Strategy: strategy.ToString(),
                CalculationVersion: calculationVersion,
                EffectiveEvidenceCount: effectiveEvidenceCount,
                TopicNodeId: topTopicNodeId,
                TopicName: topTopicName,
                MasteryPercentage: topMastery,
                ExamImportance: topScored.ExamImportance,
                EstimatedLearningMinutes: topScored.EstimatedLearningMinutes,
                EstimatedLearningHours: topScored.EstimatedLearningHours,
                WeightedRecentReasoningAverage: topScored.RecentReasoningAverage01 * 100m,
                ReasoningQualitySampleCount: topScored.ReasoningQualitySampleCount,
                ReasoningWeightSum: topScored.ReasoningWeightSum,
                ReasoningQualitySource: topScored.ReasoningQualitySource,
                PrerequisiteReadiness: topScored.PrerequisiteReadiness,
                ProbabilityOfMastery: topScored.ProbabilityOfMastery,
                ExpectedScoreGain: topScored.ExpectedScoreGain,
                RawOpportunity: topScored.RawOpportunity,
                NormalizedOpportunityScore: topScored.NormalizedOpportunityScore,
                CandidateCount: candidateResult.EligibleCandidates.Count,
                TieBreakRank: topScored.Rank,
                TieBreakFactors: "NormalizedScore DESC, CurrentMastery ASC, ExamImportance DESC, OrderIndex ASC, TopicNodeId ASC");
            calculationBreakdown = JsonSerializer.SerializeToDocument(topBreakdownDto);

            for (int i = 0; i < topCandidates.Count; i++)
            {
                var candidate = topCandidates[i];
                var itemReason = RecommendationExplanationBuilder.BuildOpportunityGapExplanation(
                    candidate.TopicName,
                    candidate.NormalizedOpportunityScore,
                    candidate.CurrentMastery,
                    candidate.ExamImportance,
                    candidate.PrerequisiteReadiness);

                var itemQuestion = await _questionSelector.SelectQuestionAsync(
                    centerId,
                    studentId,
                    candidate.TopicNodeId,
                    candidate.CurrentMastery,
                    cancellationToken);

                if (i == 0)
                {
                    topQuestion = itemQuestion;
                }

                var candidateBreakdownDto = new OpportunityGapBreakdown(
                    Strategy: strategy.ToString(),
                    CalculationVersion: calculationVersion,
                    EffectiveEvidenceCount: effectiveEvidenceCount,
                    TopicNodeId: candidate.TopicNodeId,
                    TopicName: candidate.TopicName,
                    MasteryPercentage: candidate.CurrentMastery,
                    ExamImportance: candidate.ExamImportance,
                    EstimatedLearningMinutes: candidate.EstimatedLearningMinutes,
                    EstimatedLearningHours: candidate.EstimatedLearningHours,
                    WeightedRecentReasoningAverage: candidate.RecentReasoningAverage01 * 100m,
                    ReasoningQualitySampleCount: candidate.ReasoningQualitySampleCount,
                    ReasoningWeightSum: candidate.ReasoningWeightSum,
                    ReasoningQualitySource: candidate.ReasoningQualitySource,
                    PrerequisiteReadiness: candidate.PrerequisiteReadiness,
                    ProbabilityOfMastery: candidate.ProbabilityOfMastery,
                    ExpectedScoreGain: candidate.ExpectedScoreGain,
                    RawOpportunity: candidate.RawOpportunity,
                    NormalizedOpportunityScore: candidate.NormalizedOpportunityScore,
                    CandidateCount: candidateResult.EligibleCandidates.Count,
                    TieBreakRank: candidate.Rank,
                    TieBreakFactors: "NormalizedScore DESC, CurrentMastery ASC, ExamImportance DESC, OrderIndex ASC, TopicNodeId ASC");

                pathItems.Add(new LearningPathItem
                {
                    LearningPathItemId = GenerateUlongId(),
                    CenterId = centerId,
                    TopicNodeId = candidate.TopicNodeId,
                    RecommendedQuestionId = itemQuestion?.QuestionId,
                    RankOrder = (uint)(i + 1),
                    OpportunityScore = candidate.NormalizedOpportunityScore,
                    CalculationBreakdown = JsonSerializer.SerializeToDocument(candidateBreakdownDto),
                    Reason = itemReason,
                    Status = i == 0 ? LearningPathItemStatus.Current : LearningPathItemStatus.Pending,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }
        }
        else
        {
            // Mode C: LinearFallback (< 3 effective evidence, or candidates locked, or reasoning sample unavailable)
            strategy = LearningPathStrategy.LinearFallback;
            recType = RecommendationType.LinearFallback;
            calculationVersion = "linear-v1";

            var linearResult = _linearFallbackSelector.SelectTopics(
                candidateResult.AllActiveTopicNodes,
                candidateResult.MasteryByTopicNodeId,
                candidateResult.PrerequisitesByTopicNodeId);

            if (linearResult.IsBlocked || linearResult.SelectedTopics.Count == 0)
            {
                // Prerequisite graph blocked or cycle: cleanly supersede active artifacts and commit
                await SupersedeActiveArtifactsAsync(centerId, studentId, subjectId, utcNow, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transactionB.CommitAsync(cancellationToken);
                return RecommendationGenerationResult.Blocked(LinearFallbackResult.PrerequisiteGraphBlocked);
            }

            var topNode = linearResult.SelectedTopics[0];
            topTopicNodeId = topNode.NodeId;
            topTopicName = topNode.NodeName;
            topMastery = candidateResult.MasteryByTopicNodeId.GetValueOrDefault(topTopicNodeId, 0m);
            explanation = RecommendationExplanationBuilder.BuildLinearFallbackExplanation(effectiveEvidenceCount);

            calculationBreakdown = JsonSerializer.SerializeToDocument(new
            {
                CalculationVersion = calculationVersion,
                Strategy = strategy.ToString(),
                TopTopicNodeId = topTopicNodeId,
                TopTopicName = topTopicName,
                CurrentMastery = topMastery,
                EffectiveEvidenceCount = effectiveEvidenceCount,
                ReasoningSampleUnavailable = candidateResult.SubjectReasoningSampleCount == 0
            });

            for (int i = 0; i < linearResult.SelectedTopics.Count; i++)
            {
                var node = linearResult.SelectedTopics[i];
                var m = candidateResult.MasteryByTopicNodeId.GetValueOrDefault(node.NodeId, 0m);
                var itemQuestion = await _questionSelector.SelectQuestionAsync(
                    centerId,
                    studentId,
                    node.NodeId,
                    m,
                    cancellationToken);

                if (i == 0)
                {
                    topQuestion = itemQuestion;
                }

                var itemBreakdown = JsonSerializer.SerializeToDocument(new
                {
                    CalculationVersion = calculationVersion,
                    Strategy = strategy.ToString(),
                    TopicNodeId = node.NodeId,
                    TopicName = node.NodeName,
                    CurrentMastery = m,
                    EffectiveEvidenceCount = effectiveEvidenceCount,
                    ReasoningSampleUnavailable = candidateResult.SubjectReasoningSampleCount == 0
                });

                pathItems.Add(new LearningPathItem
                {
                    LearningPathItemId = GenerateUlongId(),
                    CenterId = centerId,
                    TopicNodeId = node.NodeId,
                    RecommendedQuestionId = itemQuestion?.QuestionId,
                    RankOrder = (uint)(i + 1),
                    OpportunityScore = null,
                    CalculationBreakdown = itemBreakdown,
                    Reason = explanation,
                    Status = i == 0 ? LearningPathItemStatus.Current : LearningPathItemStatus.Pending,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                });
            }
        }

        // 5. Query latest version number for learning path
        var maxVersion = await _dbContext.LearningPaths
            .Where(lp => lp.CenterId == centerId
                && lp.StudentId == studentId
                && lp.SubjectId == subjectId)
            .Select(lp => (uint?)lp.Version)
            .MaxAsync(cancellationToken) ?? 0u;

        // 6. Supersede previous active artifacts
        await SupersedeActiveArtifactsAsync(centerId, studentId, subjectId, utcNow, cancellationToken);

        // 7. Create new LearningPath and Recommendation
        var learningPath = new LearningPath
        {
            LearningPathId = Guid.NewGuid(),
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            Strategy = strategy,
            Version = maxVersion + 1,
            Status = LearningPathStatus.Active,
            GeneratedFromAttemptId = sourceAttemptId,
            GeneratedAt = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Items = pathItems
        };

        var recommendation = new Recommendation
        {
            RecommendationId = GenerateUlongId(),
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TopicNodeId = topTopicNodeId,
            QuestionId = topQuestion?.QuestionId,
            RecommendationType = recType,
            OpportunityScore = topOpportunityScore,
            CalculationVersion = calculationVersion,
            CalculationBreakdown = calculationBreakdown,
            Explanation = explanation,
            SourceAttemptId = sourceAttemptId,
            Status = RecommendationStatus.Active,
            GeneratedAt = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        _dbContext.LearningPaths.Add(learningPath);
        _dbContext.Recommendations.Add(recommendation);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transactionB.CommitAsync(cancellationToken);

        return RecommendationGenerationResult.Success(recommendation);
    }

    private async Task SupersedeActiveArtifactsAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existingActiveRecs = await _dbContext.Recommendations
            .Where(r => r.CenterId == centerId
                && r.StudentId == studentId
                && r.SubjectId == subjectId
                && r.Status == RecommendationStatus.Active
                && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var activeRec in existingActiveRecs)
        {
            activeRec.Status = RecommendationStatus.Superseded;
            activeRec.UpdatedAt = utcNow;
        }

        var existingActivePaths = await _dbContext.LearningPaths
            .Where(lp => lp.CenterId == centerId
                && lp.StudentId == studentId
                && lp.SubjectId == subjectId
                && lp.Status == LearningPathStatus.Active
                && !lp.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var activePath in existingActivePaths)
        {
            activePath.Status = LearningPathStatus.Superseded;
            activePath.UpdatedAt = utcNow;
        }
    }

    public async Task<RecommendationOperationResult> AcceptAsync(
        Guid centerId,
        Guid studentId,
        ulong recommendationId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await StudentLockHelper.AcquireStudentLockAsync(_dbContext, centerId, studentId, cancellationToken);

        var rec = await _dbContext.Recommendations
            .SingleOrDefaultAsync(
                r => r.CenterId == centerId
                    && r.StudentId == studentId
                    && r.RecommendationId == recommendationId
                    && !r.IsDeleted,
                cancellationToken);

        if (rec is null)
        {
            return RecommendationOperationResult.FailNotFound("Recommendation not found.");
        }

        if (rec.Status == RecommendationStatus.Accepted)
        {
            return RecommendationOperationResult.Ok(rec);
        }

        if (rec.Status != RecommendationStatus.Active)
        {
            return RecommendationOperationResult.FailConflict(
                "RECOMMENDATION_NOT_ACTIVE",
                $"Cannot accept recommendation with status '{rec.Status}'.");
        }

        rec.Status = RecommendationStatus.Accepted;
        rec.UpdatedAt = utcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RecommendationOperationResult.Ok(rec);
    }

    public async Task<RecommendationOperationResult> DismissAsync(
        Guid centerId,
        Guid studentId,
        ulong recommendationId,
        string? reason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await StudentLockHelper.AcquireStudentLockAsync(_dbContext, centerId, studentId, cancellationToken);

        var rec = await _dbContext.Recommendations
            .SingleOrDefaultAsync(
                r => r.CenterId == centerId
                    && r.StudentId == studentId
                    && r.RecommendationId == recommendationId
                    && !r.IsDeleted,
                cancellationToken);

        if (rec is null)
        {
            return RecommendationOperationResult.FailNotFound("Recommendation not found.");
        }

        if (rec.Status == RecommendationStatus.Dismissed)
        {
            return RecommendationOperationResult.Ok(rec);
        }

        if (rec.Status != RecommendationStatus.Active)
        {
            return RecommendationOperationResult.FailConflict(
                "RECOMMENDATION_NOT_ACTIVE",
                $"Cannot dismiss recommendation with status '{rec.Status}'.");
        }

        rec.Status = RecommendationStatus.Dismissed;
        rec.UpdatedAt = utcNow;

        // State Machine: Update LearningPath
        var activePath = await _dbContext.LearningPaths
            .Include(lp => lp.Items)
            .SingleOrDefaultAsync(
                lp => lp.CenterId == centerId
                    && lp.StudentId == studentId
                    && lp.SubjectId == rec.SubjectId
                    && lp.Status == LearningPathStatus.Active
                    && !lp.IsDeleted,
                cancellationToken);

        if (activePath is not null)
        {
            var currentItem = activePath.Items
                .FirstOrDefault(i => i.Status == LearningPathItemStatus.Current && !i.IsDeleted);

            if (currentItem is not null)
            {
                currentItem.Status = LearningPathItemStatus.Skipped;
                currentItem.UpdatedAt = utcNow;
            }

            var nextPendingItem = activePath.Items
                .Where(i => i.Status == LearningPathItemStatus.Pending && !i.IsDeleted)
                .OrderBy(i => i.RankOrder)
                .FirstOrDefault();

            if (nextPendingItem is not null)
            {
                nextPendingItem.Status = LearningPathItemStatus.Current;
                nextPendingItem.UpdatedAt = utcNow;

                // Candidate snapshot promotion: DO NOT rerun question selector or recompute breakdown!
                // Promote stored snapshot directly from nextPendingItem.
                var promotedBreakdown = nextPendingItem.CalculationBreakdown ?? JsonSerializer.SerializeToDocument(new
                {
                    Strategy = activePath.Strategy.ToString(),
                    CalculationVersion = rec.CalculationVersion,
                    TopicNodeId = nextPendingItem.TopicNodeId,
                    SnapshotUnavailable = true,
                    PromotedFromRank = nextPendingItem.RankOrder
                });

                var newRec = new Recommendation
                {
                    RecommendationId = GenerateUlongId(),
                    CenterId = centerId,
                    StudentId = studentId,
                    SubjectId = rec.SubjectId,
                    TopicNodeId = nextPendingItem.TopicNodeId,
                    QuestionId = nextPendingItem.RecommendedQuestionId,
                    RecommendationType = activePath.Strategy == LearningPathStrategy.LinearFallback
                        ? RecommendationType.LinearFallback
                        : RecommendationType.TopicAndQuestion,
                    OpportunityScore = nextPendingItem.OpportunityScore,
                    CalculationVersion = rec.CalculationVersion,
                    CalculationBreakdown = promotedBreakdown,
                    Explanation = nextPendingItem.Reason,
                    SourceAttemptId = rec.SourceAttemptId,
                    Status = RecommendationStatus.Active,
                    GeneratedAt = utcNow,
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                };

                _dbContext.Recommendations.Add(newRec);
            }
            else
            {
                // No pending items remaining -> Completed
                activePath.Status = LearningPathStatus.Completed;
                activePath.UpdatedAt = utcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RecommendationOperationResult.Ok(rec);
    }

    public async Task<NextQuestionDto?> GetNextQuestionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        // Strictly read-only query: no persistence, no entity mutation
        var activePath = await _dbContext.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.TopicNode)
            .Include(lp => lp.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.RecommendedQuestion)
            .SingleOrDefaultAsync(
                lp => lp.CenterId == centerId
                    && lp.StudentId == studentId
                    && lp.SubjectId == subjectId
                    && lp.Status == LearningPathStatus.Active
                    && !lp.IsDeleted,
                cancellationToken);

        if (activePath is null)
        {
            return null;
        }

        var currentItem = activePath.Items
            .FirstOrDefault(i => i.Status == LearningPathItemStatus.Current && !i.IsDeleted);

        if (currentItem is null)
        {
            return null;
        }

        var topicTwin = await _dbContext.KnowledgeTwins
            .AsNoTracking()
            .SingleOrDefaultAsync(
                kt => kt.CenterId == centerId
                    && kt.StudentId == studentId
                    && kt.TopicNodeId == currentItem.TopicNodeId
                    && !kt.IsDeleted,
                cancellationToken);
        decimal topicMastery = topicTwin?.MasteryPercentage ?? 0m;

        var rec = await _dbContext.Recommendations
            .AsNoTracking()
            .Where(r => r.CenterId == centerId
                && r.StudentId == studentId
                && r.SubjectId == subjectId
                && r.TopicNodeId == currentItem.TopicNodeId
                && (r.Status == RecommendationStatus.Active || r.Status == RecommendationStatus.Accepted)
                && !r.IsDeleted)
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new NextQuestionDto
        {
            Strategy = activePath.Strategy,
            RecommendationId = rec?.RecommendationId,
            Topic = new NextQuestionTopicDto(
                NodeId: currentItem.TopicNodeId,
                NodeName: currentItem.TopicNode?.NodeName ?? "",
                Mastery: topicMastery),
            Question = currentItem.RecommendedQuestion is not null
                ? new NextQuestionQuestionDto(
                    QuestionId: currentItem.RecommendedQuestion.QuestionId,
                    QuestionType: currentItem.RecommendedQuestion.QuestionType,
                    Difficulty: currentItem.RecommendedQuestion.Difficulty,
                    QuestionText: currentItem.RecommendedQuestion.QuestionText,
                    EstimatedTimeSeconds: currentItem.RecommendedQuestion.EstimatedTimeSeconds,
                    ReasoningRequired: currentItem.RecommendedQuestion.ReasoningRequired,
                    LanguageCode: currentItem.RecommendedQuestion.LanguageCode)
                : null,
            Explanation = currentItem.Reason
        };
    }

    public async Task<Recommendation?> GetActiveRecommendationAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Recommendations
            .Include(r => r.TopicNode)
            .Include(r => r.Question)
            .Where(r => r.CenterId == centerId
                && r.StudentId == studentId
                && r.Status == RecommendationStatus.Active
                && !r.IsDeleted);

        if (subjectId.HasValue)
        {
            query = query.Where(r => r.SubjectId == subjectId.Value);
        }

        return await query
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LearningPath?> GetActiveLearningPathAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.LearningPaths
            .Include(lp => lp.Items.Where(i => !i.IsDeleted).OrderBy(i => i.RankOrder))
                .ThenInclude(i => i.TopicNode)
            .Include(lp => lp.Items.Where(i => !i.IsDeleted).OrderBy(i => i.RankOrder))
                .ThenInclude(i => i.RecommendedQuestion)
            .Where(lp => lp.CenterId == centerId
                && lp.StudentId == studentId
                && lp.Status == LearningPathStatus.Active
                && !lp.IsDeleted);

        if (subjectId.HasValue)
        {
            query = query.Where(lp => lp.SubjectId == subjectId.Value);
        }

        return await query
            .OrderByDescending(lp => lp.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
