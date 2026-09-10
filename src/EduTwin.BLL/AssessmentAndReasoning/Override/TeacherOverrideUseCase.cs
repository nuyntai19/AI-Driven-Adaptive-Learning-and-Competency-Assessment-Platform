using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
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
    private readonly TimeProvider _timeProvider;

    public TeacherOverrideUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IEvidenceGate evidenceGate,
        IEvidenceAssessmentFactory evidenceAssessmentFactory,
        IStudentGoalRiskUpdater goalRiskUpdater,
        IStudentTwinUpdater studentTwinUpdater,
        ITwinUpdateHistoryWriter historyWriter,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _evidenceGate = evidenceGate ?? throw new ArgumentNullException(nameof(evidenceGate));
        _evidenceAssessmentFactory = evidenceAssessmentFactory ?? throw new ArgumentNullException(nameof(evidenceAssessmentFactory));
        _goalRiskUpdater = goalRiskUpdater ?? throw new ArgumentNullException(nameof(goalRiskUpdater));
        _studentTwinUpdater = studentTwinUpdater ?? throw new ArgumentNullException(nameof(studentTwinUpdater));
        _historyWriter = historyWriter ?? throw new ArgumentNullException(nameof(historyWriter));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
        var teacherId = _tenantContext.UserId.Value;
        var role = _tenantContext.Role;

        // 1. Validate request parameters
        if (request.ReasoningQuality < 0m || request.ReasoningQuality > 100m)
        {
            return TeacherOverrideResult.ValidationFailed("INVALID_REASONING_QUALITY", "Reasoning quality must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return TeacherOverrideResult.ValidationFailed("REASON_REQUIRED", "Override reason is required.");
        }

        // 2. Load ReasoningAnalysis with Attempt and Question
        var analysis = await _dbContext.ReasoningAnalyses
            .Include(ra => ra.Attempt)
                .ThenInclude(att => att.Question)
            .SingleOrDefaultAsync(
                ra => ra.CenterId == centerId && ra.AnalysisId == analysisId,
                cancellationToken);

        if (analysis is null)
        {
            return TeacherOverrideResult.NotFound();
        }

        // 3. Validate Teacher Ownership (unless CenterManager)
        if (!string.Equals(role, nameof(UserRole.CenterManager), StringComparison.OrdinalIgnoreCase))
        {
            var isClassTeacher = await _dbContext.ClassStudents
                .AnyAsync(
                    cs => cs.CenterId == centerId
                        && cs.StudentId == analysis.Attempt.StudentId
                        && cs.Status == ClassStudentStatus.Active
                        && cs.Class.TeacherId == teacherId,
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
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var newOverrideVersion = analysis.OverrideVersion + 1;

            // A. Update ReasoningAnalysis override fields
            analysis.OverrideReasoningQuality = request.ReasoningQuality;
            analysis.OverrideErrorType = request.ErrorType;
            analysis.OverrideFeedback = request.Feedback;
            analysis.OverrideIsCorrect = request.IsCorrect;
            analysis.OverrideReason = request.Reason;
            analysis.OverriddenByTeacherId = teacherId;
            analysis.OverriddenAt = now;
            analysis.OverrideVersion = newOverrideVersion;
            analysis.NeedsTeacherReview = false;
            analysis.UpdatedAt = now;

            // B. Update Attempt
            var attempt = analysis.Attempt;
            attempt.IsCorrect = request.IsCorrect;
            attempt.Status = AttemptStatus.Completed;
            attempt.UpdatedAt = now;

            // C. Find previous latest EvidenceAssessment for this attempt
            var previousEvidence = await _dbContext.EvidenceAssessments
                .Where(e => e.CenterId == centerId && e.AttemptId == attempt.AttemptId)
                .OrderByDescending(e => e.EvaluatedAt)
                .ThenByDescending(e => e.EvidenceAssessmentId)
                .FirstOrDefaultAsync(cancellationToken);

            // D. Evaluate Gate for TeacherOverride
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
                teacherId);

            _dbContext.EvidenceAssessments.Add(newEvidence);

            // E. Replay all attempts for this student and topic chronologically
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

            var attemptIds = topicAttempts.Select(ta => ta.Attempt.AttemptId).ToArray();
            var analyses = await _dbContext.ReasoningAnalyses
                .Where(ra => ra.CenterId == centerId && attemptIds.Contains(ra.AttemptId))
                .ToListAsync(cancellationToken);
            var analysesByAttempt = analyses.ToDictionary(ra => ra.AttemptId);

            var persistedEvidences = await _dbContext.EvidenceAssessments
                .Where(ea => ea.CenterId == centerId && attemptIds.Contains(ea.AttemptId))
                .OrderBy(ea => ea.EvaluatedAt)
                .ThenBy(ea => ea.EvidenceAssessmentId)
                .ToListAsync(cancellationToken);
            var latestEvidenceByAttempt = persistedEvidences
                .GroupBy(ea => ea.AttemptId)
                .ToDictionary(g => g.Key, g => g.Last());

            // Recompute BehaviorTwin ConfidenceCalibration so that changes to attempt correctness (e.g. Essay null -> true/false)
            // are reflected with the true denominator of graded attempts before replaying topic mastery
            var behaviorTwin = await _dbContext.BehaviorTwins
                .SingleOrDefaultAsync(
                    b => b.CenterId == centerId && b.StudentId == studentId && b.SubjectId == subjectId && !b.IsDeleted,
                    cancellationToken);

            if (behaviorTwin is not null)
            {
                var attemptsWithQuestions = await (
                    from a in _dbContext.Attempts
                    join q in _dbContext.Questions on new { a.CenterId, a.QuestionId } equals new { q.CenterId, q.QuestionId }
                    where a.CenterId == centerId
                        && a.StudentId == studentId
                        && q.SubjectId == subjectId
                    select new { a.AttemptId, a.Confidence, a.IsCorrect }
                ).ToListAsync(cancellationToken);

                var calibratedList = attemptsWithQuestions
                    .Select(a => new
                    {
                        a.Confidence,
                        IsCorrect = a.AttemptId == attempt.AttemptId ? (bool?)request.IsCorrect : a.IsCorrect
                    })
                    .Where(a => a.IsCorrect != null)
                    .ToList();

                if (calibratedList.Count > 0)
                {
                    var totalCalib = calibratedList.Sum(ca => Math.Clamp(100m - Math.Abs(ca.Confidence - (ca.IsCorrect == true ? 100m : 0m)), 0m, 100m));
                    behaviorTwin.ConfidenceCalibration = Math.Round(totalCalib / calibratedList.Count, 2, MidpointRounding.AwayFromZero);
                    behaviorTwin.UpdatedAt = now;
                }
            }

            var calibration = behaviorTwin is not null
                ? Math.Clamp(behaviorTwin.ConfidenceCalibration / 100m, 0m, 1m)
                : 0.50m;

            decimal replayedMastery = 0m;
            int replayedCount = 0;
            int effectiveEvidenceCount = 0;
            MasteryCalculationResult? overrideCalcResult = null;

            foreach (var item in topicAttempts)
            {
                var att = item.Attempt;
                var q = item.Question;
                var a = analysesByAttempt.GetValueOrDefault(att.AttemptId);

                // Use the persisted EvidenceAssessment as the sole authoritative source of truth for reasoning weight
                var ev = att.AttemptId == attempt.AttemptId
                    ? newEvidence
                    : latestEvidenceByAttempt.GetValueOrDefault(att.AttemptId);

                decimal reasoningWeight = ev?.ReasoningWeight ?? 0m;
                decimal? effectiveQuality = a is not null
                    ? (a.OverrideVersion > 0 ? a.OverrideReasoningQuality : a.ReasoningQuality)
                    : null;

                bool effectiveCorrectness = att.IsCorrect ?? false;
                if (att.AttemptId == attempt.AttemptId)
                {
                    effectiveCorrectness = request.IsCorrect;
                }

                if (reasoningWeight > 0m)
                {
                    effectiveEvidenceCount++;
                }

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
                    ConfidenceCalibration: reasoningWeight > 0m && effectiveQuality.HasValue ? calibration : null,
                    Difficulty: q.Difficulty);

                var calcResult = MasteryCalculator.Calculate(calcInput);
                replayedMastery = calcResult.NewMastery;
                replayedCount++;

                if (att.AttemptId == attempt.AttemptId)
                {
                    overrideCalcResult = calcResult;
                }
            }

            // Update or create KnowledgeTwin with consistent EvidenceCount (only positive-weight evidence counted)
            var knowledgeTwin = await _dbContext.KnowledgeTwins
                .SingleOrDefaultAsync(
                    k => k.CenterId == centerId && k.StudentId == studentId && k.SubjectId == subjectId && k.TopicNodeId == topicNodeId && !k.IsDeleted,
                    cancellationToken);

            decimal previousMastery = 0m;
            if (knowledgeTwin is null)
            {
                knowledgeTwin = new KnowledgeTwin
                {
                    CenterId = centerId,
                    StudentId = studentId,
                    SubjectId = subjectId,
                    TopicNodeId = topicNodeId,
                    MasteryPercentage = replayedMastery,
                    EvidenceCount = (uint)effectiveEvidenceCount,
                    LastReasoningQuality = request.ReasoningQuality,
                    LastAttemptId = attempt.AttemptId,
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
                knowledgeTwin.LastReasoningQuality = request.ReasoningQuality;
                knowledgeTwin.LastAttemptId = attempt.AttemptId;
                knowledgeTwin.LastEvidenceAt = now;
                knowledgeTwin.UpdatedAt = now;
            }

            // Update StudentSubjectGoal Risk Score
            var goal = await _goalRiskUpdater.UpdateAsync(centerId, studentId, subjectId, now, cancellationToken);
            decimal newRiskScore = goal?.RiskScore ?? 0m;

            // Update StudentTwin
            await _studentTwinUpdater.UpdateAsync(centerId, studentId, now, cancellationToken);

            // Append TwinUpdateHistory using actual calculation breakdown from replay
            var historyBreakdown = overrideCalcResult?.Breakdown ?? new MasteryCalculationBreakdown(
                IsFallback: false,
                PreviousMastery: previousMastery,
                NormalizedReasoningQuality: request.ReasoningQuality / 100m,
                ReasoningWeight: 1.00m,
                Correctness: request.IsCorrect ? 1m : 0m,
                TimeQuality: 1.00m,
                ConfidenceCalibration: calibration,
                Difficulty: attempt.Question.Difficulty,
                DifficultyMultiplier: 1.00m,
                LearningRate: 0.25m,
                EvidenceTarget: request.ReasoningQuality,
                UnclampedNewMastery: replayedMastery,
                NewMastery: replayedMastery,
                Delta: replayedMastery - previousMastery);

            var historyCalcResult = new MasteryCalculationResult(
                PreviousMastery: previousMastery,
                NewMastery: replayedMastery,
                Delta: replayedMastery - previousMastery,
                EffectiveReasoningQuality: request.ReasoningQuality,
                CalculationVersion: MasteryCalculator.CalculationVersion,
                Breakdown: historyBreakdown,
                Explanation: $"Teacher override applied by {teacherId}: {request.Reason} (Replayed {replayedCount} attempts, {effectiveEvidenceCount} effective evidence records).");

            await _historyWriter.WriteAsync(
                centerId,
                studentId,
                subjectId,
                topicNodeId,
                attempt.AttemptId,
                analysis.AnalysisId,
                TwinEventSource.TeacherOverride,
                historyCalcResult,
                now,
                teacherId,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var responseData = new TeacherOverrideDataDto
            {
                AnalysisId = analysis.AnalysisId.ToString(CultureInfo.InvariantCulture),
                HasTeacherOverride = true,
                OverrideVersion = newOverrideVersion,
                OverriddenAt = now,
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

            return TeacherOverrideResult.Success(responseData);
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
    }
}
