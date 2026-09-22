using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public sealed class TeacherApproveUseCase : ITeacherApproveUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IEvidenceGate _evidenceGate;
    private readonly IEvidenceAssessmentFactory _evidenceAssessmentFactory;
    private readonly IStudentGoalRiskUpdater _goalRiskUpdater;
    private readonly IStudentTwinUpdater _studentTwinUpdater;
    private readonly ITwinUpdateHistoryWriter _historyWriter;
    private readonly IBehaviorCalibrationCalculator _calibrationCalculator;
    private readonly IBehaviorCalibrationSampleProvider _calibrationSampleProvider;
    private readonly IRecommendationEngine? _recommendationEngine;
    private readonly TimeProvider _timeProvider;
    private readonly IAttemptTeacherReviewScopeGuard _scopeGuard;
    private readonly ILogger<TeacherApproveUseCase> _logger;
    private readonly IOverallAssignmentCommentWorkflow? _overallCommentWorkflow;

    public TeacherApproveUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IEvidenceGate evidenceGate,
        IEvidenceAssessmentFactory evidenceAssessmentFactory,
        IStudentGoalRiskUpdater goalRiskUpdater,
        IStudentTwinUpdater studentTwinUpdater,
        ITwinUpdateHistoryWriter historyWriter,
        TimeProvider timeProvider,
        IBehaviorCalibrationCalculator? calibrationCalculator = null,
        IBehaviorCalibrationSampleProvider? calibrationSampleProvider = null,
        IRecommendationEngine? recommendationEngine = null,
        IAttemptTeacherReviewScopeGuard? scopeGuard = null,
        ILogger<TeacherApproveUseCase>? logger = null,
        IOverallAssignmentCommentWorkflow? overallCommentWorkflow = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _evidenceGate = evidenceGate ?? throw new ArgumentNullException(nameof(evidenceGate));
        _evidenceAssessmentFactory = evidenceAssessmentFactory ?? throw new ArgumentNullException(nameof(evidenceAssessmentFactory));
        _goalRiskUpdater = goalRiskUpdater ?? throw new ArgumentNullException(nameof(goalRiskUpdater));
        _studentTwinUpdater = studentTwinUpdater ?? throw new ArgumentNullException(nameof(studentTwinUpdater));
        _historyWriter = historyWriter ?? throw new ArgumentNullException(nameof(historyWriter));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _calibrationCalculator = calibrationCalculator ?? new BehaviorCalibrationCalculator();
        _calibrationSampleProvider = calibrationSampleProvider ?? new BehaviorCalibrationSampleProvider(_dbContext);
        _recommendationEngine = recommendationEngine;
        _scopeGuard = scopeGuard ?? new AttemptTeacherReviewScopeGuard(_dbContext);
        _logger = logger ?? NullLogger<TeacherApproveUseCase>.Instance;
        _overallCommentWorkflow = overallCommentWorkflow;
    }

    public async Task<TeacherApproveResult> ExecuteAsync(
        ulong analysisId,
        TeacherApproveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return TeacherApproveResult.Forbidden();
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var role = _tenantContext.Role ?? string.Empty;

        var isTeacher = string.Equals(role, nameof(UserRole.Teacher), StringComparison.OrdinalIgnoreCase);
        var isCenterManager = string.Equals(role, nameof(UserRole.CenterManager), StringComparison.OrdinalIgnoreCase);
        if (!isTeacher && !isCenterManager)
        {
            return TeacherApproveResult.Forbidden();
        }

        if (request.Note != null && request.Note.Length > 1000)
        {
            return TeacherApproveResult.ValidationFailed("NOTE_TOO_LONG", "Ghi chú của giáo viên không được vượt quá 1000 ký tự.");
        }

        var analysis = await _dbContext.ReasoningAnalyses
            .Include(ra => ra.Attempt)
                .ThenInclude(att => att.Question)
            .SingleOrDefaultAsync(
                ra => ra.CenterId == centerId && ra.AnalysisId == analysisId,
                cancellationToken);

        if (analysis is null || analysis.Attempt is null || analysis.Attempt.Question is null)
        {
            return TeacherApproveResult.NotFound();
        }

        var attempt = analysis.Attempt;
        var question = attempt.Question;

        var canAccess = await _scopeGuard.CanAccessAttemptAsync(
            centerId,
            actorId,
            role,
            attempt,
            cancellationToken);

        if (!canAccess)
        {
            return TeacherApproveResult.Forbidden();
        }

        // Optimistic concurrency check
        if (analysis.OverrideVersion != request.OverrideVersion)
        {
            return TeacherApproveResult.Conflict();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        TeacherApproveDataDto? committedResponse = null;
        Guid recommendationStudentId = default;
        Guid recommendationSubjectId = default;
        ulong recommendationAttemptId = default;
        DateTime recommendationTriggerAt = default;

        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var newOverrideVersion = analysis.OverrideVersion + 1;

            var effectiveCorrectness = analysis.OverrideIsCorrect ?? attempt.IsCorrect;
            var effectiveScore = analysis.OverrideAwardedScore ?? attempt.AwardedScore ?? (effectiveCorrectness == true ? question.MaxScore : 0m);

            // 1. Update ReasoningAnalysis review fields
            analysis.ReviewDecision = TeacherReviewDecision.Approved;
            analysis.ReviewedByUserId = actorId;
            analysis.ReviewedAt = now;
            analysis.TeacherReviewNote = request.Note;
            analysis.NeedsTeacherReview = false;
            analysis.OverrideVersion = newOverrideVersion;
            analysis.UpdatedAt = now;

            // 2. Set Attempt Status to Completed
            attempt.Status = AttemptStatus.Completed;
            attempt.UpdatedAt = now;

            // 3. Update StudentAssignmentProgress and invalidate overall comment
            if (attempt.AssignmentId.HasValue)
            {
                var progress = await _dbContext.StudentAssignmentProgresses
                    .SingleOrDefaultAsync(
                        p => p.CenterId == attempt.CenterId
                            && p.AssignmentId == attempt.AssignmentId.Value
                            && p.StudentId == attempt.StudentId
                            && !p.IsDeleted,
                        cancellationToken);

                if (progress is not null)
                {
                    progress.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Pending;
                    progress.FinalReviewedByUserId = null;
                    progress.FinalReviewedAt = null;
                    progress.FinalTeacherNote = null;
                    progress.FinalReviewVersion++;
                    progress.IsOverallAiCommentStale = true; // Invalidate cached comment
                    progress.UpdatedAt = now;
                }
            }

            // 4. Record append-only TeacherReviewHistory audit trail
            var reviewHistory = new TeacherReviewHistory
            {
                CenterId = centerId,
                AnalysisId = analysis.AnalysisId,
                AttemptId = attempt.AttemptId,
                TeacherId = actorId,
                Decision = TeacherReviewDecision.Approved,
                PreviousScore = attempt.AwardedScore,
                NewScore = effectiveScore,
                PreviousIsCorrect = attempt.IsCorrect,
                NewIsCorrect = effectiveCorrectness,
                Note = request.Note,
                OverrideVersion = newOverrideVersion,
                CreatedAt = now,
                CreatedBy = actorId
            };
            _dbContext.TeacherReviewHistories.Add(reviewHistory);

            // 5. Authoritative Human-Confirmed Evidence Gate Evaluation
            var previousEvidence = await _dbContext.EvidenceAssessments
                .Where(e => e.CenterId == centerId && e.AttemptId == attempt.AttemptId)
                .OrderByDescending(e => e.EvaluatedAt)
                .ThenByDescending(e => e.EvidenceAssessmentId)
                .FirstOrDefaultAsync(cancellationToken);

            var gateDecision = _evidenceGate.Evaluate(new EvidenceGateInput(
                SourceType: EvidenceSourceType.TeacherApproval,
                StructuralValidationPassed: true,
                SemanticValidationPassed: true,
                HasContradiction: false,
                HasAnomaly: false,
                HasRequiredEvidence: true,
                EffectiveIsCorrect: effectiveCorrectness,
                AnalysisConfidence: null,
                AnalysisOverrideVersion: newOverrideVersion,
                IsPostFeedback: attempt.IsPostFeedback));

            var newEvidence = _evidenceAssessmentFactory.Create(
                attempt,
                analysis,
                previousEvidence,
                gateDecision,
                now,
                actorId);

            _dbContext.EvidenceAssessments.Add(newEvidence);

            // 6. Resolve pending student review requests
            var pendingReviewRequest = await _dbContext.StudentReviewRequests
                .Where(r => r.CenterId == centerId && r.AttemptId == attempt.AttemptId && r.Status == StudentReviewRequestStatus.Pending)
                .FirstOrDefaultAsync(cancellationToken);

            if (pendingReviewRequest != null)
            {
                pendingReviewRequest.Status = StudentReviewRequestStatus.Resolved;
                pendingReviewRequest.TeacherNote = request.Note ?? "Giáo viên đã duyệt kết quả.";
                pendingReviewRequest.ResolvedByTeacherId = actorId;
                pendingReviewRequest.ResolvedAt = now;
            }

            // 7. Chronological Replay for Digital Twin
            var studentId = attempt.StudentId;
            var subjectId = question.SubjectId;
            var topicNodeId = question.PrimaryTopicNodeId;

            var topicAttempts = await (
                from att in _dbContext.Attempts
                join q in _dbContext.Questions on new { att.CenterId, att.QuestionId } equals new { q.CenterId, q.QuestionId }
                where att.CenterId == centerId
                    && att.StudentId == studentId
                    && q.PrimaryTopicNodeId == topicNodeId
                orderby att.CreatedAt, att.AttemptId
                select new { Attempt = att, Question = q }
            ).ToListAsync(cancellationToken);

            var topicAttemptIds = topicAttempts.Select(ta => ta.Attempt.AttemptId).ToArray();
            var topicAnalyses = await _dbContext.ReasoningAnalyses
                .Where(ra => ra.CenterId == centerId && topicAttemptIds.Contains(ra.AttemptId))
                .ToListAsync(cancellationToken);
            var topicAnalysesByAttempt = topicAnalyses.ToDictionary(ra => ra.AttemptId);

            var persistedEvidences = await _dbContext.EvidenceAssessments
                .Where(ea => ea.CenterId == centerId && topicAttemptIds.Contains(ea.AttemptId))
                .OrderBy(ea => ea.EvaluatedAt)
                .ThenBy(ea => ea.EvidenceAssessmentId)
                .ToListAsync(cancellationToken);
            var latestEvidenceByAttempt = persistedEvidences
                .GroupBy(ea => ea.AttemptId)
                .ToDictionary(g => g.Key, g => g.Last());

            var subjectSamples = await _calibrationSampleProvider.GetSubjectSamplesAsync(
                centerId,
                studentId,
                subjectId,
                attempt,
                hasPendingCorrectnessOverride: true,
                pendingCorrectnessOverride: effectiveCorrectness,
                cancellationToken);

            var gradedSubjectAttempts = subjectSamples
                .Where(x => x.EffectiveIsCorrect.HasValue)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.AttemptId)
                .ToList();

            decimal replayedMastery = 0m;
            int replayedCount = 0;
            int effectiveEvidenceCount = 0;
            decimal? latestEffectiveReasoningQuality = null;
            ulong? latestEffectiveAttemptId = null;
            var replayedSteps = new List<ReplayStepBreakdown>(topicAttempts.Count);

            foreach (var item in topicAttempts)
            {
                var att = item.Attempt;
                var q = item.Question;
                var a = topicAnalysesByAttempt.GetValueOrDefault(att.AttemptId);

                var ev = att.AttemptId == attempt.AttemptId
                    ? newEvidence
                    : latestEvidenceByAttempt.GetValueOrDefault(att.AttemptId);

                decimal reasoningWeight = ev?.ReasoningWeight ?? 0m;
                decimal? quality = a is not null
                    ? (a.OverrideVersion > 0 ? a.OverrideReasoningQuality : a.ReasoningQuality)
                    : null;

                bool? correctness = att.AttemptId == attempt.AttemptId
                    ? effectiveCorrectness
                    : (a?.OverrideIsCorrect ?? att.IsCorrect);

                if (reasoningWeight > 0m)
                {
                    effectiveEvidenceCount++;
                    latestEffectiveReasoningQuality = quality;
                    latestEffectiveAttemptId = att.AttemptId;
                }

                var rollingWindow = gradedSubjectAttempts
                    .Where(x => x.CreatedAt < att.CreatedAt || (x.CreatedAt == att.CreatedAt && x.AttemptId <= att.AttemptId))
                    .Select(x => new GradedAttemptSample(x.Confidence, x.EffectiveIsCorrect!.Value))
                    .ToList();
                var stepCalibration = _calibrationCalculator.CalculateCalibration(rollingWindow);

                var timeRatio = q.EstimatedTimeSeconds > 0
                    ? att.TimeSpentSeconds / (decimal)q.EstimatedTimeSeconds
                    : 1m;
                var timeQuality = Math.Clamp(1m - (Math.Abs(timeRatio - 1m) * 0.5m), 0m, 1m);
                timeQuality = Math.Round(timeQuality, 2, MidpointRounding.AwayFromZero);

                var calcInput = new MasteryCalculationInput(
                    CurrentMastery: replayedMastery,
                    ReasoningQuality: quality,
                    ReasoningWeight: reasoningWeight,
                    IsCorrect: correctness,
                    TimeQuality: timeQuality,
                    ConfidenceCalibration: reasoningWeight > 0m && quality.HasValue ? stepCalibration / 100m : null,
                    Difficulty: q.Difficulty);

                var calcResult = MasteryCalculator.Calculate(calcInput);
                var prevStepMastery = replayedMastery;
                replayedMastery = calcResult.NewMastery;
                replayedCount++;

                replayedSteps.Add(new ReplayStepBreakdown(
                    AttemptId: att.AttemptId,
                    EffectiveReasoningQuality: quality,
                    ReasoningWeight: reasoningWeight,
                    EffectiveCorrectness: correctness,
                    TimeQuality: timeQuality,
                    RollingCalibration: stepCalibration,
                    Difficulty: q.Difficulty,
                    DifficultyMultiplier: calcResult.Breakdown.DifficultyMultiplier,
                    LearningRate: calcResult.Breakdown.LearningRate,
                    PreviousMastery: prevStepMastery,
                    NewMastery: calcResult.NewMastery,
                    Delta: calcResult.Delta));
            }

            var behaviorTwin = await _dbContext.BehaviorTwins
                .SingleOrDefaultAsync(
                    b => b.CenterId == centerId && b.StudentId == studentId && b.SubjectId == subjectId && !b.IsDeleted,
                    cancellationToken);

            var finalCalibration = _calibrationCalculator.CalculateCalibration(
                gradedSubjectAttempts.Select(x => new GradedAttemptSample(x.Confidence, x.EffectiveIsCorrect!.Value)));

            if (behaviorTwin is not null)
            {
                behaviorTwin.ConfidenceCalibration = finalCalibration;
                behaviorTwin.UpdatedAt = now;
            }

            var knowledgeTwin = await _dbContext.KnowledgeTwins
                .SingleOrDefaultAsync(
                    k => k.CenterId == centerId && k.StudentId == studentId && k.SubjectId == subjectId && k.TopicNodeId == topicNodeId && !k.IsDeleted,
                    cancellationToken);

            decimal previousMastery = 0m;
            if (knowledgeTwin is null)
            {
                knowledgeTwin = new KnowledgeTwin
                {
                    KnowledgeTwinId = TwinAggregateIdGenerator.NewId(),
                    CenterId = centerId,
                    StudentId = studentId,
                    SubjectId = subjectId,
                    TopicNodeId = topicNodeId,
                    MasteryPercentage = replayedMastery,
                    EvidenceCount = (uint)effectiveEvidenceCount,
                    LastReasoningQuality = latestEffectiveReasoningQuality,
                    LastAttemptId = latestEffectiveAttemptId,
                    LastEvidenceAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.KnowledgeTwins.Add(knowledgeTwin);
            }
            else
            {
                previousMastery = knowledgeTwin.MasteryPercentage;
                knowledgeTwin.MasteryPercentage = replayedMastery;
                knowledgeTwin.EvidenceCount = (uint)effectiveEvidenceCount;
                knowledgeTwin.LastReasoningQuality = latestEffectiveReasoningQuality;
                knowledgeTwin.LastAttemptId = latestEffectiveAttemptId;
                knowledgeTwin.LastEvidenceAt = now;
                knowledgeTwin.UpdatedAt = now;
            }

            var goal = await _goalRiskUpdater.UpdateAsync(centerId, studentId, subjectId, now, cancellationToken);
            decimal newRiskScore = goal?.RiskScore ?? 0m;

            await _studentTwinUpdater.UpdateAsync(centerId, studentId, now, cancellationToken);

            var historyBreakdown = new MasteryCalculationBreakdown(
                IsFallback: false,
                PreviousMastery: previousMastery,
                NormalizedReasoningQuality: (analysis.ReasoningQuality ?? 80m) / 100m,
                ReasoningWeight: 1.00m,
                Correctness: effectiveCorrectness == true ? 1m : 0m,
                TimeQuality: 1.00m,
                ConfidenceCalibration: finalCalibration / 100m,
                Difficulty: question.Difficulty,
                DifficultyMultiplier: 1.00m,
                LearningRate: 0.25m,
                EvidenceTarget: analysis.ReasoningQuality ?? 80m,
                UnclampedNewMastery: replayedMastery,
                NewMastery: replayedMastery,
                Delta: replayedMastery - previousMastery,
                ReplaySteps: replayedSteps);

            var replaySummary = new ReplaySummaryBreakdown(
                TriggerAttemptId: attempt.AttemptId,
                PreviousMastery: previousMastery,
                FinalMastery: replayedMastery,
                ReplayCount: replayedCount,
                EffectiveEvidenceCount: effectiveEvidenceCount,
                FinalCalibration: finalCalibration,
                ReplaySteps: replayedSteps);

            var historyCalcResult = new MasteryCalculationResult(
                PreviousMastery: previousMastery,
                NewMastery: replayedMastery,
                Delta: replayedMastery - previousMastery,
                EffectiveReasoningQuality: latestEffectiveReasoningQuality,
                CalculationVersion: "replay-v1",
                Breakdown: historyBreakdown,
                Explanation: $"Teacher approval confirmed by {actorId} on attempt {attempt.AttemptId}. (Replayed {replayedCount} attempts; final mastery {replayedMastery}%).",
                HistoryBreakdown: replaySummary);

            await _historyWriter.WriteAsync(
                centerId,
                studentId,
                subjectId,
                topicNodeId,
                attempt.AttemptId,
                analysis.AnalysisId,
                TwinEventSource.Replay,
                historyCalcResult,
                now,
                actorId,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            committedResponse = new TeacherApproveDataDto
            {
                AnalysisId = analysis.AnalysisId.ToString(CultureInfo.InvariantCulture),
                AttemptId = attempt.AttemptId.ToString(CultureInfo.InvariantCulture),
                ReviewDecision = TeacherReviewDecision.Approved,
                NeedsTeacherReview = false,
                OverrideVersion = newOverrideVersion,
                ReviewedAt = now,
                TeacherNote = request.Note,
                EffectiveAwardedScore = effectiveScore,
                EffectiveIsCorrect = effectiveCorrectness == true,
                Replay = new TeacherOverrideReplayDto
                {
                    StudentId = studentId.ToString("D"),
                    TopicNodeId = topicNodeId.ToString(CultureInfo.InvariantCulture),
                    AttemptsReplayed = replayedCount,
                    PreviousMastery = previousMastery,
                    NewMastery = replayedMastery,
                    NewRiskScore = newRiskScore,
                    RecommendationRecalculated = false
                }
            };

            recommendationStudentId = studentId;
            recommendationSubjectId = subjectId;
            recommendationAttemptId = attempt.AttemptId;
            recommendationTriggerAt = now;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            return TeacherApproveResult.Conflict();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (_recommendationEngine is not null)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                var recResult = await _recommendationEngine.GenerateAndPersistAsync(
                    centerId,
                    recommendationStudentId,
                    recommendationSubjectId,
                    recommendationAttemptId,
                    recommendationTriggerAt,
                    timeout.Token);

                committedResponse!.Replay.RecommendationRecalculated =
                    recResult.Status == RecommendationGenerationStatus.Generated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Recommendation generation failed after teacher approval commit for attempt {AttemptId}.",
                    recommendationAttemptId);
            }
        }

        if (_overallCommentWorkflow is not null && attempt.AssignmentId.HasValue)
        {
            await _overallCommentWorkflow.GenerateAndCacheOverallCommentAsync(centerId, attempt.AssignmentId.Value, attempt.StudentId, cancellationToken);
        }

        return TeacherApproveResult.Success(committedResponse!);
    }
}
