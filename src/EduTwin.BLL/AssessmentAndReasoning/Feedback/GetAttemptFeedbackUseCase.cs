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
using EduTwin.Contracts.Assignments;
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
            .Include(a => a.Attachment)
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

        // Load ReasoningAnalysis with teacher user info
        var analysis = await _dbContext.ReasoningAnalyses.AsNoTracking()
            .Include(ra => ra.OverriddenByUser)
            .Include(ra => ra.ReviewedByUser)
            .Where(ra => ra.CenterId == centerId && ra.AttemptId == attemptId)
            .FirstOrDefaultAsync(cancellationToken);

        // Load Student Review Request if any
        var reviewRequest = await _dbContext.StudentReviewRequests.AsNoTracking()
            .Where(r => r.CenterId == centerId && r.AttemptId == attemptId)
            .OrderByDescending(r => r.RequestId)
            .FirstOrDefaultAsync(cancellationToken);

        var pendingRequest = await _dbContext.StudentReviewRequests.AsNoTracking()
            .AnyAsync(r => r.CenterId == centerId && r.AttemptId == attemptId &&
                r.Status == StudentReviewRequestStatus.Pending, cancellationToken);
        var assignmentApproved = attempt.AssignmentId.HasValue &&
            await _dbContext.StudentAssignmentProgresses.AsNoTracking().AnyAsync(p =>
                p.CenterId == centerId && p.AssignmentId == attempt.AssignmentId &&
                p.StudentId == attempt.StudentId && !p.IsDeleted &&
                p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved, cancellationToken);
        var voided = attempt.AssignmentId.HasValue &&
            await _dbContext.AssignmentQuestions.AsNoTracking().AnyAsync(q =>
                q.CenterId == centerId && q.AssignmentId == attempt.AssignmentId &&
                q.QuestionId == attempt.QuestionId && q.IsVoided, cancellationToken);
        var job = await _dbContext.AIAnalysisJobs.AsNoTracking()
            .Where(j => j.CenterId == centerId && j.AttemptId == attemptId)
            .OrderByDescending(j => j.AnalysisJobId).FirstOrDefaultAsync(cancellationToken);

        // The UI polls this endpoint during analysis. Such reads must neither
        // expose the solution nor mutate the attempt snapshot held by the worker.
        // A teacher viewing a submission is not a student viewing the solution.
        var isStudent = currentUserRole == nameof(UserRole.Student);
        var canExposeSolution = attempt.Status is AttemptStatus.Completed
                or AttemptStatus.NeedsTeacherReview or AttemptStatus.AnalysisFailed
            && job?.Status is not (AIJobStatus.Pending or AIJobStatus.Processing);
        if (isStudent && canExposeSolution && !attempt.SolutionExposedAt.HasValue)
        {
            var tracked = await _dbContext.Attempts
                .FirstOrDefaultAsync(a => a.CenterId == centerId && a.AttemptId == attemptId, cancellationToken);
            if (tracked != null && !tracked.SolutionExposedAt.HasValue
                && tracked.Status is AttemptStatus.Completed or AttemptStatus.NeedsTeacherReview or AttemptStatus.AnalysisFailed)
            {
                tracked.SolutionExposedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

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
        var hasTeacherGrade = analysis?.OverrideIsCorrect.HasValue == true
            || analysis?.OverrideAwardedScore.HasValue == true;
        var gradingSource = hasTeacherGrade
            ? "Teacher"
            : attempt.IsCorrect.HasValue || attempt.AwardedScore.HasValue
                ? "Deterministic"
                : "PendingTeacher";
        var gradingReasonCode = hasTeacherGrade
            ? PreliminaryGradingReasonCodes.TeacherOverride
            : (analysis?.IsFallback == true && string.IsNullOrEmpty(attempt.PreliminaryGradingReasonCode))
                ? PreliminaryGradingReasonCodes.AiProviderUnavailable
                : attempt.PreliminaryGradingReasonCode;

        var finalAnswerDisplay = attempt.FinalAnswer;
        string? answerDisplayLatex = attempt.AnswerDisplayLatex;
        if (attempt.Question?.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice)
        {
            var options = await _dbContext.QuestionOptions
                .AsNoTracking()
                .Where(o => o.CenterId == centerId && o.QuestionId == attempt.QuestionId && !o.IsDeleted)
                .OrderBy(o => o.OrderIndex)
                .ToListAsync(cancellationToken);

            var raw = attempt.FinalAnswer?.Trim() ?? string.Empty;
            var matched = options.FirstOrDefault(o =>
                o.OptionId.ToString(CultureInfo.InvariantCulture) == raw ||
                string.Equals(o.OptionLabel, raw, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(o.OptionText, raw, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                finalAnswerDisplay = !string.IsNullOrWhiteSpace(matched.OptionText)
                    ? $"{matched.OptionLabel}. {matched.OptionText}"
                    : matched.OptionLabel;
            }
            else if (!string.IsNullOrWhiteSpace(raw))
            {
                // Fail-closed for ANY unmatched MultipleChoice answer (numeric option ID, UUID, or obsolete string)
                finalAnswerDisplay = "Phương án đã chọn";
            }

            answerDisplayLatex = null;
        }

        // 1. Student Submission
        var studentSubmissionDto = new AttemptFeedbackStudentSubmissionDto
        {
            FinalAnswer = finalAnswerDisplay,
            AnswerDisplayLatex = answerDisplayLatex,
            ReasoningText = attempt.ReasoningText,
            Confidence = attempt.Confidence,
            TimeSpentSeconds = attempt.TimeSpentSeconds,
            AnswerChanges = attempt.AnswerChanges,
            AttachmentUrl = attempt.Attachment != null
                ? $"/api/v1/learning/attempts/{attempt.AttemptId}/attachment"
                : null
        };

        // 2. Teacher Solution & Reference
        AttemptFeedbackTeacherSolutionDto? teacherSolutionDto = null;
        if (attempt.Question != null && (!isStudent || canExposeSolution))
        {
            AttemptFeedbackGradingCriteriaDto? criteriaDto = null;
            if (attempt.Question.GradingCriteria != null)
            {
                criteriaDto = new AttemptFeedbackGradingCriteriaDto
                {
                    ScoringNotes = attempt.Question.GradingCriteria.ScoringNotes ?? string.Empty,
                    RequiredIdeas = attempt.Question.GradingCriteria.RequiredIdeas != null
                        ? new List<string>(attempt.Question.GradingCriteria.RequiredIdeas)
                        : new List<string>(),
                    CommonErrors = attempt.Question.GradingCriteria.CommonErrors != null
                        ? new List<string>(attempt.Question.GradingCriteria.CommonErrors)
                        : new List<string>()
                };
            }

            teacherSolutionDto = new AttemptFeedbackTeacherSolutionDto
            {
                CorrectAnswer = attempt.Question.CorrectAnswer,
                Solution = attempt.Question.Solution,
                ExpectedReasoning = attempt.Question.ExpectedReasoning,
                GradingCriteria = criteriaDto
            };
        }

        // 3. AI Analysis
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

            int? aiReasoningQualityInt = analysis.ReasoningQuality.HasValue
                ? (int)Math.Round(analysis.ReasoningQuality.Value, MidpointRounding.AwayFromZero)
                : null;

            analysisDto = new AttemptFeedbackAnalysisDto
            {
                AnalysisId = analysis.AnalysisId.ToString(CultureInfo.InvariantCulture),
                SchemaVersion = analysis.SchemaVersion,
                MethodDetected = analysis.MethodDetected,
                ReasoningQuality = aiReasoningQualityInt,
                QualityBand = GetQualityBand(analysis.ReasoningQuality),
                ErrorType = analysis.ErrorType.ToString(),
                Misconception = analysis.Misconception,
                MissingSteps = missingSteps,
                RootCauseNodes = rootCauseNodes,
                Confidence = analysis.AnalysisConfidence.HasValue
                    ? (int)Math.Round(analysis.AnalysisConfidence.Value, MidpointRounding.AwayFromZero)
                    : null,
                // Legacy builder substituted canned correctness messages for AI
                // feedback. Do not surface a stale wrong statement after a regrade.
                Feedback = AnalysisFeedbackPresentation.Resolve(analysis.FeedbackOrigin, analysis.Feedback, effectiveCorrectness),
                IsFallback = analysis.IsFallback,
                NeedsTeacherReview = analysis.NeedsTeacherReview,
                HasTeacherOverride = analysis.OverrideVersion > 0,
                IsRawAI = analysis.FeedbackOrigin is "Gemini" or "Groq",
                FeedbackOrigin = analysis.FeedbackOrigin,
                AnswerAssessment = analysis.AnswerAssessment,
                ReasoningVerdict = analysis.ReasoningVerdict,
                Model = analysis.ModelName ?? "Gemini AI",
                SolutionType = analysis.SolutionType,
                AiSolution = analysis.AiSolution,
                SuggestedScore = analysis.SuggestedScore,
                SuggestedRubricGrade = RubricGrade.Deserialize(analysis.SuggestedRubricGradeJson),
                UsesAlternativeMethod = analysis.UsesAlternativeMethod
            };
        }

        // 4. Teacher Final Evaluation (Override or Approval)
        AttemptFeedbackTeacherEvaluationDto? teacherEvaluationDto = null;
        if (analysis != null && (analysis.OverrideVersion > 0 || !string.IsNullOrEmpty(analysis.ReviewDecision)))
        {
            var reviewer = analysis.ReviewedByUser ?? analysis.OverriddenByUser;
            var teacherName = reviewer?.DisplayName ?? reviewer?.Username ?? "Giáo viên";
            var isApprovedAsIs = string.Equals(analysis.ReviewDecision, "Approved", StringComparison.OrdinalIgnoreCase) && analysis.OverrideVersion == 0;
            int? teacherReasoningQualityInt = analysis.OverrideReasoningQuality.HasValue
                ? (int)Math.Round(analysis.OverrideReasoningQuality.Value, MidpointRounding.AwayFromZero)
                : null;

            teacherEvaluationDto = new AttemptFeedbackTeacherEvaluationDto
            {
                RubricGrade = RubricGrade.Deserialize(await _dbContext.TeacherReviewHistories.AsNoTracking()
                    .Where(h => h.CenterId == centerId && h.AnalysisId == analysis.AnalysisId)
                    .OrderByDescending(h => h.OverrideVersion).ThenByDescending(h => h.HistoryId)
                    .Select(h => h.RubricResultJson).FirstOrDefaultAsync(cancellationToken)),
                HasTeacherOverride = analysis.OverrideVersion > 0,
                IsApprovedAsIs = isApprovedAsIs,
                ReviewDecision = analysis.ReviewDecision,
                TeacherReviewNote = analysis.TeacherReviewNote,
                TeacherIsCorrect = analysis.OverrideIsCorrect ?? attempt.IsCorrect,
                TeacherScore = analysis.OverrideAwardedScore ?? attempt.AwardedScore,
                TeacherFeedback = analysis.OverrideFeedback ?? analysis.TeacherReviewNote,
                TeacherReasoningQuality = teacherReasoningQualityInt,
                TeacherErrorType = analysis.OverrideErrorType?.ToString(),
                ReviewedByTeacherName = teacherName,
                ReviewedAt = analysis.ReviewedAt ?? analysis.OverriddenAt,
                OriginalAIRawGrade = new AttemptFeedbackGradingDto
                {
                    IsCorrect = attempt.IsCorrect,
                    AwardedScore = attempt.AwardedScore,
                    MaxScore = maxScore,
                    ReasonCode = attempt.PreliminaryGradingReasonCode,
                    Source = attempt.IsCorrect.HasValue || attempt.AwardedScore.HasValue
                        ? "Deterministic"
                        : "PendingTeacher"
                }
            };
        }

        // 5. Student Review Request
        StudentReviewRequestDto? reviewRequestDto = null;
        if (reviewRequest != null)
        {
            reviewRequestDto = new StudentReviewRequestDto
            {
                RequestId = reviewRequest.RequestId,
                AttemptId = reviewRequest.AttemptId,
                StudentId = reviewRequest.StudentId,
                QuestionId = reviewRequest.QuestionId,
                StudentComment = reviewRequest.StudentComment,
                Status = reviewRequest.Status,
                TeacherNote = reviewRequest.TeacherNote,
                ResolvedByTeacherId = reviewRequest.ResolvedByTeacherId,
                ResolvedAt = reviewRequest.ResolvedAt,
                CreatedAt = reviewRequest.CreatedAt
            };
        }

        // 6. Retry Quota & Cooldown
        const byte maxManualRetries = 3;
        const int cooldownPeriodSeconds = 30;
        var retriesUsed = attempt.ManualRetryCount;
        var retriesRemaining = (byte)Math.Max(0, maxManualRetries - retriesUsed);

        var cooldownRemaining = 0;
        DateTime? nextRetryAllowedAt = null;
        if (attempt.LastManualRetryAt.HasValue)
        {
            var elapsedSeconds = (DateTime.UtcNow - attempt.LastManualRetryAt.Value).TotalSeconds;
            if (elapsedSeconds < cooldownPeriodSeconds)
            {
                cooldownRemaining = (int)Math.Ceiling(cooldownPeriodSeconds - elapsedSeconds);
                nextRetryAllowedAt = attempt.LastManualRetryAt.Value.AddSeconds(cooldownPeriodSeconds);
            }
        }

        var retryEligible = AttemptFeedbackActionPolicy.CanRetryAI(
            attempt, job, analysis, assignmentApproved, pendingRequest, voided);
        var canRetry = retryEligible && retriesRemaining > 0 && cooldownRemaining == 0;
        var retryQuotaDto = new RetryQuotaDto
        {
            IsEligible = retryEligible,
            ManualRetriesUsed = retriesUsed,
            ManualRetriesRemaining = retriesRemaining,
            CooldownRemainingSeconds = cooldownRemaining,
            CanRetry = canRetry,
            NextRetryAllowedAt = nextRetryAllowedAt
        };

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
                MaxScore = maxScore,
                ReasonCode = gradingReasonCode,
                Source = gradingSource
            },
            StudentSubmission = studentSubmissionDto,
            TeacherSolution = teacherSolutionDto,
            Analysis = analysisDto,
            TeacherFinalEvaluation = teacherEvaluationDto,
            ReviewRequest = reviewRequestDto,
            RetryQuota = retryQuotaDto,
            AIProcessing = job is null ? null : new AttemptAIProcessingDto
            {
                Status = job.Status.ToString(),
                Reason = AIProcessingPresentation.Reason(job.LastErrorCode),
                NextAttemptAt = job.Status == AIJobStatus.Pending ? job.AvailableAt : null
            },
            Actions = new AttemptFeedbackActionsDto
            {
                CanRequestTeacherReview = AttemptFeedbackActionPolicy.CanRequestReview(
                    attempt, analysis, assignmentApproved, pendingRequest, voided),
                CanReportQuestion = AttemptFeedbackActionPolicy.CanReportQuestion(attempt, pendingRequest, voided)
            },
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
