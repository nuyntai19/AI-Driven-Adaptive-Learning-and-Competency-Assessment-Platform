using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.AssessmentAndReasoning.Override;

public sealed class TeacherOverrideUseCase : ITeacherOverrideUseCase
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
    private readonly ILogger<TeacherOverrideUseCase> _logger;

    public TeacherOverrideUseCase(
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
        ILogger<TeacherOverrideUseCase>? logger = null)
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
        _logger = logger ?? NullLogger<TeacherOverrideUseCase>.Instance;
    }

    public async Task<TeacherOverrideResult> ExecuteAsync(
        ulong analysisId,
        TeacherOverrideRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_tenantContext.IsResolved || _tenantContext.CenterId is null || _tenantContext.UserId is null)
        {
            return TeacherOverrideResult.Forbidden();
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var role = _tenantContext.Role;

        var isTeacher = string.Equals(role, nameof(UserRole.Teacher), StringComparison.OrdinalIgnoreCase);
        var isCenterManager = string.Equals(role, nameof(UserRole.CenterManager), StringComparison.OrdinalIgnoreCase);
        if (!isTeacher && !isCenterManager)
        {
            return TeacherOverrideResult.Forbidden();
        }

        // 1. Validate request parameters
        if (request.ReasoningQuality < 0m || request.ReasoningQuality > 100m)
        {
            return TeacherOverrideResult.ValidationFailed("INVALID_REASONING_QUALITY", "Reasoning quality must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TeacherOverrideResult.ValidationFailed("REASON_REQUIRED", "Override reason is required.");
        }

        if (request.Reason.Length > 1000)
        {
            return TeacherOverrideResult.ValidationFailed("REASON_TOO_LONG", "Override reason must not exceed 1000 characters.");
        }

        if (!Enum.IsDefined(typeof(ErrorType), request.ErrorType))
        {
            return TeacherOverrideResult.ValidationFailed("INVALID_ERROR_TYPE", "ErrorType is invalid.");
        }

        // 2. Load ReasoningAnalysis with Attempt and Question
        var analysis = await _dbContext.ReasoningAnalyses
            .Include(ra => ra.Attempt)
                .ThenInclude(att => att.Question)
            .SingleOrDefaultAsync(
                ra => ra.CenterId == centerId && ra.AnalysisId == analysisId,
                cancellationToken);

        if (analysis is null || analysis.Attempt is null || analysis.Attempt.Question is null)
        {
            return TeacherOverrideResult.NotFound();
        }

        var attempt = analysis.Attempt;
        var question = attempt.Question;

        if (request.AwardedScore.HasValue)
        {
            var maxScore = question.MaxScore;
            if (request.AwardedScore.Value < 0m || request.AwardedScore.Value > maxScore)
            {
                return TeacherOverrideResult.ValidationFailed("INVALID_AWARDED_SCORE", $"Awarded score must be between 0 and {maxScore}.");
            }
        }

        // 3. Validate Teacher Ownership (unless CenterManager)
        if (isTeacher)
        {
            var isClassTeacher = await _dbContext.ClassStudents
                .AnyAsync(
                    cs => cs.CenterId == centerId
                        && cs.StudentId == attempt.StudentId
                        && cs.Status == ClassStudentStatus.Active
                        && cs.Class.TeacherId == actorId,
                    cancellationToken);

            if (!isClassTeacher)
            {
                return TeacherOverrideResult.Forbidden();
            }
        }

        // 4. Optimistic concurrency check on OverrideVersion
        if (analysis.OverrideVersion != request.OverrideVersion)
        {
            return TeacherOverrideResult.Conflict();
        }

        // 5. Transactional Override & Replay
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await StudentLockHelper.AcquireStudentLockAsync(_dbContext, centerId, attempt.StudentId, cancellationToken);
        TeacherOverrideDataDto? committedResponse = null;
        Guid recommendationStudentId = default;
        Guid recommendationSubjectId = default;
        ulong recommendationAttemptId = default;
        DateTime recommendationTriggerAt = default;
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var newOverrideVersion = analysis.OverrideVersion + 1;

            // A. Update ReasoningAnalysis override fields (full-state semantics: null AwardedScore clears override)
            analysis.OverrideReasoningQuality = request.ReasoningQuality;
            analysis.OverrideErrorType = request.ErrorType;
            analysis.OverrideFeedback = request.Feedback;
            analysis.OverrideIsCorrect = request.IsCorrect;
            analysis.OverrideAwardedScore = request.AwardedScore;
            analysis.OverrideReason = request.Reason;
            analysis.OverriddenByUserId = actorId;
            analysis.OverriddenAt = now;
            analysis.OverrideVersion = newOverrideVersion;
            analysis.NeedsTeacherReview = false;
            analysis.UpdatedAt = now;

            // B. Update Attempt Status without mutating preliminary IsCorrect or AwardedScore!
            attempt.Status = AttemptStatus.Completed;
            attempt.UpdatedAt = now;

            // C. Update StudentAssignmentProgress if attempt belongs to an assignment
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
                    var dbQuestionIds = await _dbContext.Attempts
                        .Where(a => a.CenterId == attempt.CenterId
                            && a.AssignmentId == attempt.AssignmentId.Value
                            && a.StudentId == attempt.StudentId
                            && (a.Status == AttemptStatus.Completed
                                || a.Status == AttemptStatus.NeedsTeacherReview
                                || a.AttemptId == attempt.AttemptId))
                        .Select(a => a.QuestionId)
                        .ToListAsync(cancellationToken);

                    var localQuestionIds = _dbContext.Attempts.Local
                        .Where(a => a.CenterId == attempt.CenterId
                            && a.AssignmentId == attempt.AssignmentId.Value
                            && a.StudentId == attempt.StudentId
                            && (a.Status == AttemptStatus.Completed
                                || a.Status == AttemptStatus.NeedsTeacherReview
                                || a.AttemptId == attempt.AttemptId))
                        .Select(a => a.QuestionId);

                    var answeredQuestionCount = dbQuestionIds
                        .Concat(localQuestionIds)
                        .Append(attempt.QuestionId)
                        .Distinct()
                        .Count();

                    progress.CompletedQuestionCount = (uint)answeredQuestionCount;
                    if (progress.CompletedQuestionCount >= progress.TotalQuestionCount && progress.TotalQuestionCount > 0)
                    {
                        progress.Status = ProgressStatus.Completed;
                        progress.CompletedAt ??= now;
                    }
                    else if (progress.Status == ProgressStatus.NotStarted)
                    {
                        progress.Status = ProgressStatus.InProgress;
                        progress.StartedAt ??= now;
                    }
                    progress.UpdatedAt = now;
                }
            }

            // D. Find previous latest EvidenceAssessment for this attempt
            var previousEvidence = await _dbContext.EvidenceAssessments
                .Where(e => e.CenterId == centerId && e.AttemptId == attempt.AttemptId)
                .OrderByDescending(e => e.EvaluatedAt)
                .ThenByDescending(e => e.EvidenceAssessmentId)
                .FirstOrDefaultAsync(cancellationToken);

            // E. Evaluate Gate for TeacherOverride
            var gateDecision = _evidenceGate.Evaluate(new EvidenceGateInput(
                SourceType: EvidenceSourceType.TeacherOverride,
                StructuralValidationPassed: true,
                SemanticValidationPassed: true,
                HasContradiction: false,
                HasAnomaly: false,
                HasRequiredEvidence: true,
                EffectiveIsCorrect: request.IsCorrect,
                AnalysisConfidence: null,
                AnalysisOverrideVersion: newOverrideVersion));

            var newEvidence = _evidenceAssessmentFactory.Create(
                attempt,
                analysis,
                previousEvidence,
                gateDecision,
                now,
                actorId);

            _dbContext.EvidenceAssessments.Add(newEvidence);

            // F. Replay all attempts for this student and topic chronologically
            var studentId = attempt.StudentId;
            var subjectId = attempt.Question.SubjectId;
            var topicNodeId = attempt.Question.PrimaryTopicNodeId;

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

            // G. Load all attempts in the subject to build subject-wide chronological rolling calibration
            var subjectSamples = await _calibrationSampleProvider.GetSubjectSamplesAsync(
                centerId,
                studentId,
                subjectId,
                attempt,
                hasPendingCorrectnessOverride: true,
                pendingCorrectnessOverride: request.IsCorrect,
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

                // Use the persisted EvidenceAssessment as the sole authoritative source of truth for reasoning weight
                var ev = att.AttemptId == attempt.AttemptId
                    ? newEvidence
                    : latestEvidenceByAttempt.GetValueOrDefault(att.AttemptId);

                decimal reasoningWeight = ev?.ReasoningWeight ?? 0m;
                decimal? effectiveQuality = a is not null
                    ? (a.OverrideVersion > 0 ? a.OverrideReasoningQuality : a.ReasoningQuality)
                    : null;

                bool? effectiveCorrectness;
                if (att.AttemptId == attempt.AttemptId)
                {
                    effectiveCorrectness = request.IsCorrect;
                }
                else
                {
                    effectiveCorrectness = a?.OverrideIsCorrect ?? att.IsCorrect;
                }

                // Fail closed if positive weight evidence has unresolved correctness
                if (reasoningWeight > 0m && !effectiveCorrectness.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Attempt {att.AttemptId} on topic {topicNodeId} has positive evidence weight ({reasoningWeight}) but unresolved correctness.");
                }

                if (reasoningWeight > 0m)
                {
                    effectiveEvidenceCount++;
                    latestEffectiveReasoningQuality = effectiveQuality;
                    latestEffectiveAttemptId = att.AttemptId;
                }

                // Subject-wide rolling calibration up to (CreatedAt, AttemptId)
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
                    ReasoningQuality: effectiveQuality,
                    ReasoningWeight: reasoningWeight,
                    IsCorrect: effectiveCorrectness,
                    TimeQuality: timeQuality,
                    ConfidenceCalibration: reasoningWeight > 0m && effectiveQuality.HasValue ? stepCalibration / 100m : null,
                    Difficulty: q.Difficulty);

                var calcResult = MasteryCalculator.Calculate(calcInput);
                var prevStepMastery = replayedMastery;
                replayedMastery = calcResult.NewMastery;
                replayedCount++;

                replayedSteps.Add(new ReplayStepBreakdown(
                    AttemptId: att.AttemptId,
                    EffectiveReasoningQuality: effectiveQuality,
                    ReasoningWeight: reasoningWeight,
                    EffectiveCorrectness: effectiveCorrectness,
                    TimeQuality: timeQuality,
                    RollingCalibration: stepCalibration,
                    Difficulty: q.Difficulty,
                    DifficultyMultiplier: calcResult.Breakdown.DifficultyMultiplier,
                    LearningRate: calcResult.Breakdown.LearningRate,
                    PreviousMastery: prevStepMastery,
                    NewMastery: calcResult.NewMastery,
                    Delta: calcResult.Delta));
            }

            // H. Update BehaviorTwin with final calibration across all graded subject attempts
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

            // I. Update or create KnowledgeTwin with consistent EvidenceCount
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

            // J. Update StudentSubjectGoal Risk Score & StudentTwin
            var goal = await _goalRiskUpdater.UpdateAsync(centerId, studentId, subjectId, now, cancellationToken);
            decimal newRiskScore = goal?.RiskScore ?? 0m;

            await _studentTwinUpdater.UpdateAsync(centerId, studentId, now, cancellationToken);

            // K. Append TwinUpdateHistory using a replay summary, never a synthetic single-step breakdown.
            var historyBreakdown = new MasteryCalculationBreakdown(
                IsFallback: false,
                PreviousMastery: previousMastery,
                NormalizedReasoningQuality: request.ReasoningQuality / 100m,
                ReasoningWeight: 1.00m,
                Correctness: request.IsCorrect ? 1m : 0m,
                TimeQuality: 1.00m,
                ConfidenceCalibration: finalCalibration / 100m,
                Difficulty: attempt.Question.Difficulty,
                DifficultyMultiplier: 1.00m,
                LearningRate: 0.25m,
                EvidenceTarget: request.ReasoningQuality,
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
                Explanation: $"Teacher override applied by {actorId} on attempt {attempt.AttemptId}: {request.Reason} (Replayed {replayedCount} attempts; {effectiveEvidenceCount} effective evidence records; final mastery {replayedMastery}%).",
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

            var effectiveAwardedScore = request.AwardedScore ?? attempt.AwardedScore;

            committedResponse = new TeacherOverrideDataDto
            {
                AnalysisId = analysis.AnalysisId.ToString(CultureInfo.InvariantCulture),
                HasTeacherOverride = true,
                OverrideVersion = newOverrideVersion,
                OverriddenAt = now,
                OverrideAwardedScore = request.AwardedScore,
                EffectiveAwardedScore = effectiveAwardedScore,
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
            return TeacherOverrideResult.Conflict();
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
                    "Best-effort recommendation generation failed after teacher override commit for center {CenterId}, student {StudentId}, subject {SubjectId}, attempt {AttemptId}.",
                    centerId,
                    recommendationStudentId,
                    recommendationSubjectId,
                    recommendationAttemptId);
            }
        }

        return TeacherOverrideResult.Success(committedResponse!);
    }
}
