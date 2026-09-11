using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.AssessmentAndReasoning.Feedback;

public sealed class GetAttemptFeedbackUseCase : IGetAttemptFeedbackUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _studentOwnershipGuard;

    public GetAttemptFeedbackUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard studentOwnershipGuard)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _studentOwnershipGuard = studentOwnershipGuard ?? throw new ArgumentNullException(nameof(studentOwnershipGuard));
    }

    public async Task<AttemptFeedbackResult> ExecuteAsync(ulong attemptId, CancellationToken cancellationToken)
    {
        if (attemptId == 0)
        {
            return AttemptFeedbackResult.ValidationFailed();
        }

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return AttemptFeedbackResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;
        var currentUserId = _tenantContext.UserId.Value;
        var currentUserRole = _tenantContext.Role;

        var attempt = await _dbContext.Attempts.AsNoTracking()
            .Include(a => a.Question)
            .Where(a => a.CenterId == centerId && a.AttemptId == attemptId)
            .FirstOrDefaultAsync(cancellationToken);

        if (attempt == null)
        {
            return AttemptFeedbackResult.NotFound();
        }

        // Authorization check: Student -> own attempt; Teacher/Manager -> scoped
        if (currentUserRole == nameof(UserRole.Student))
        {
            if (attempt.StudentId != currentUserId)
            {
                return AttemptFeedbackResult.Forbidden();
            }
        }
        else
        {
            var accessDecision = await _studentOwnershipGuard.CheckStudentAccessAsync(attempt.StudentId, cancellationToken);
            if (accessDecision == OwnershipDecision.NotFound)
            {
                return AttemptFeedbackResult.NotFound();
            }
            if (accessDecision == OwnershipDecision.Forbidden)
            {
                return AttemptFeedbackResult.Forbidden();
            }
        }

        // Load ReasoningAnalysis
        var analysis = await _dbContext.ReasoningAnalyses.AsNoTracking()
            .Where(ra => ra.CenterId == centerId && ra.AttemptId == attemptId)
            .FirstOrDefaultAsync(cancellationToken);

        // Load TwinChange from TwinUpdateHistory
        var twinHistory = await _dbContext.TwinUpdateHistories.AsNoTracking()
            .Include(h => h.TopicNode)
            .Where(h => h.CenterId == centerId && h.AttemptId == attemptId)
            .OrderByDescending(h => h.HistoryId)
            .FirstOrDefaultAsync(cancellationToken);

        // Load Recommendation
        var recommendation = await _dbContext.Recommendations.AsNoTracking()
            .Include(r => r.TopicNode)
            .Where(r => r.CenterId == centerId && r.SourceAttemptId == attemptId && !r.IsDeleted)
            .OrderByDescending(r => r.RecommendationId)
            .FirstOrDefaultAsync(cancellationToken);

        // Grading mapping (effective values)
        var effectiveCorrectness = analysis?.OverrideIsCorrect ?? attempt.IsCorrect;
        var effectiveScore = analysis?.OverrideAwardedScore ?? attempt.AwardedScore;
        var maxScore = attempt.Question?.MaxScore ?? 1.00m;

        AttemptFeedbackAnalysisDto? analysisDto = null;
        if (analysis != null)
        {
            var missingSteps = new List<string>();
            if (analysis.MissingSteps != null && analysis.MissingSteps.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in analysis.MissingSteps.RootElement.EnumerateArray())
                {
                    if (step.ValueKind == JsonValueKind.String && step.GetString() is { } s)
                    {
                        missingSteps.Add(s);
                    }
                }
            }

            var rootCauseNodes = new List<AttemptFeedbackRootCauseNodeDto>();
            if (analysis.RootCauseNodeIds != null && analysis.RootCauseNodeIds.RootElement.ValueKind == JsonValueKind.Array)
            {
                var nodeIds = new List<ulong>();
                foreach (var node in analysis.RootCauseNodeIds.RootElement.EnumerateArray())
                {
                    if (node.ValueKind == JsonValueKind.Number && node.TryGetUInt64(out var id))
                    {
                        nodeIds.Add(id);
                    }
                }

                if (nodeIds.Count > 0)
                {
                    var nodeMap = await _dbContext.KnowledgeNodes.AsNoTracking()
                        .Where(kn => kn.CenterId == centerId && nodeIds.Contains(kn.NodeId))
                        .ToDictionaryAsync(kn => kn.NodeId, kn => kn.NodeName, cancellationToken);

                    foreach (var id in nodeIds)
                    {
                        rootCauseNodes.Add(new AttemptFeedbackRootCauseNodeDto
                        {
                            NodeId = id.ToString(CultureInfo.InvariantCulture),
                            NodeName = nodeMap.TryGetValue(id, out var name) ? name : $"Topic {id}"
                        });
                    }
                }
            }

            var effectiveQuality = analysis.OverrideReasoningQuality ?? analysis.ReasoningQuality;
            int? reasoningQualityInt = effectiveQuality.HasValue
                ? (int)Math.Round(effectiveQuality.Value, MidpointRounding.AwayFromZero)
                : null;

            analysisDto = new AttemptFeedbackAnalysisDto
            {
                AnalysisId = analysis.AnalysisId.ToString(CultureInfo.InvariantCulture),
                SchemaVersion = analysis.SchemaVersion,
                MethodDetected = analysis.MethodDetected,
                ReasoningQuality = reasoningQualityInt,
                QualityBand = GetQualityBand(effectiveQuality),
                ErrorType = (analysis.OverrideErrorType ?? analysis.ErrorType).ToString(),
                Misconception = analysis.Misconception,
                MissingSteps = missingSteps,
                RootCauseNodes = rootCauseNodes,
                Confidence = analysis.AnalysisConfidence.HasValue
                    ? (int)Math.Round(analysis.AnalysisConfidence.Value, MidpointRounding.AwayFromZero)
                    : null,
                Feedback = analysis.OverrideFeedback ?? analysis.Feedback,
                IsFallback = analysis.IsFallback,
                NeedsTeacherReview = analysis.NeedsTeacherReview,
                HasTeacherOverride = analysis.OverrideVersion > 0
            };
        }

        AttemptFeedbackTwinChangeDto? twinChangeDto = null;
        if (twinHistory != null)
        {
            twinChangeDto = new AttemptFeedbackTwinChangeDto
            {
                TopicNodeId = twinHistory.TopicNodeId.ToString(CultureInfo.InvariantCulture),
                TopicName = twinHistory.TopicNode?.NodeName ?? $"Topic {twinHistory.TopicNodeId}",
                PreviousMastery = twinHistory.PreviousMastery,
                NewMastery = twinHistory.NewMastery,
                Delta = twinHistory.MasteryDelta,
                Explanation = twinHistory.Explanation
            };
        }

        AttemptFeedbackRecommendationDto? recommendationDto = null;
        if (recommendation != null)
        {
            recommendationDto = new AttemptFeedbackRecommendationDto
            {
                RecommendationId = recommendation.RecommendationId.ToString(CultureInfo.InvariantCulture),
                Type = recommendation.RecommendationType.ToString(),
                TopicNodeId = recommendation.TopicNodeId.ToString(CultureInfo.InvariantCulture),
                TopicName = recommendation.TopicNode?.NodeName ?? $"Topic {recommendation.TopicNodeId}",
                QuestionId = recommendation.QuestionId?.ToString(CultureInfo.InvariantCulture) ?? "",
                OpportunityScore = recommendation.OpportunityScore ?? 0m,
                Explanation = recommendation.Explanation
            };
        }

        var resultData = new AttemptFeedbackDataDto
        {
            AttemptId = attempt.AttemptId.ToString(CultureInfo.InvariantCulture),
            QuestionId = attempt.QuestionId.ToString(CultureInfo.InvariantCulture),
            Status = attempt.Status.ToString(),
            Grading = new AttemptFeedbackGradingDto
            {
                IsCorrect = effectiveCorrectness,
                AwardedScore = effectiveScore,
                MaxScore = maxScore
            },
            Analysis = analysisDto,
            TwinChange = twinChangeDto,
            Recommendation = recommendationDto
        };

        return AttemptFeedbackResult.Success(resultData);
    }

    private static string? GetQualityBand(decimal? quality)
    {
        if (!quality.HasValue) return null;
        var q = quality.Value;
        if (q >= 80m) return "Good";
        if (q >= 60m) return "Acceptable";
        if (q >= 40m) return "NeedsImprovement";
        return "Poor";
    }
}
