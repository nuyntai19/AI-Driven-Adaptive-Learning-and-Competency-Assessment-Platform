using System.Text.Json;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

/// <summary>Explicit tenant/question-scoped maintenance; not automatic mass regrading.</summary>
public sealed class MathGradingRepair(EduTwinDbContext db)
{
    public async Task<int> RepairAsync(Guid centerId, ulong questionId, CancellationToken cancellationToken)
    {
        var question = await db.Questions.SingleAsync(q => q.CenterId == centerId && q.QuestionId == questionId, cancellationToken);
        if (question.QuestionType != QuestionType.ShortAnswer || !new BoundedMathAnswerNormalizer().TryNormalize(question.CorrectAnswer, out _))
            throw new InvalidOperationException("Question is not within the bounded mathematical grammar.");
        var attempts = await db.Attempts.Where(a => a.CenterId == centerId && a.QuestionId == questionId && !a.Skipped)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.AttemptId).ToListAsync(cancellationToken);
        var changes = new List<Attempt>();
        var now = DateTime.UtcNow;
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (question.AnswerEvaluationMode != QuestionAnswerEvaluationMode.MathEquivalent)
        {
            question.AnswerEvaluationMode = QuestionAnswerEvaluationMode.MathEquivalent;
            question.RowVersion++;
            question.UpdatedAt = now;
        }
        foreach (var attempt in attempts)
        {
            var analysis = await db.ReasoningAnalyses.SingleOrDefaultAsync(a => a.CenterId == centerId && a.AttemptId == attempt.AttemptId, cancellationToken);
            if (analysis is null || analysis.OverrideVersion > 0 || analysis.ReviewedAt.HasValue) continue; // Preserve every human decision.
            var grade = new ShortAnswerGrader().Grade(attempt.FinalAnswer, question.CorrectAnswer,
                new QuestionGradingContext { EvaluationMode = QuestionAnswerEvaluationMode.MathEquivalent, MaxScore = question.MaxScore });
            if (attempt.IsCorrect == grade.IsCorrect && attempt.AwardedScore == grade.Score) continue;
            var prior = await db.EvidenceAssessments.Where(e => e.CenterId == centerId && e.AttemptId == attempt.AttemptId)
                .OrderByDescending(e => e.EvaluatedAt).ThenByDescending(e => e.EvidenceAssessmentId).FirstOrDefaultAsync(cancellationToken);
            var oldCorrectness = attempt.IsCorrect;
            var oldScore = attempt.AwardedScore;
            var oldReason = attempt.PreliminaryGradingReasonCode;
            attempt.IsCorrect = grade.IsCorrect;
            attempt.AwardedScore = grade.Score;
            attempt.PreliminaryGradingReasonCode = grade.ReasonCode;
            attempt.Status = AttemptStatus.NeedsTeacherReview;
            attempt.RowVersion++;
            attempt.UpdatedAt = now;
            analysis.NeedsTeacherReview = true;
            analysis.RowVersion++;
            analysis.UpdatedAt = now;
            // Original AI observation and old evidence remain intact. The old AI
            // was anchored to a bad grade: do not trust its mastery weight blindly.
            db.EvidenceAssessments.Add(new EvidenceAssessmentFactory().Create(attempt, analysis, prior,
                new EvidenceGateDecision(EvidenceSourceType.RuleFallback, EvidenceTrustLevel.ReviewOnly,
                    EvidenceDecisionMode.DeterministicOnly, 0m,
                    ["MATH_CANONICALIZATION_CORRECTION", $"PREVIOUS_CORRECTNESS:{oldCorrectness}", $"PREVIOUS_SCORE:{oldScore}", $"PREVIOUS_REASON:{oldReason}"],
                    true, "math-regrade-v1", analysis.OverrideVersion), now, null));
            changes.Add(attempt);
            if (attempt.AssignmentId.HasValue)
            {
                var progress = await db.StudentAssignmentProgresses.SingleOrDefaultAsync(p => p.CenterId == centerId && p.StudentId == attempt.StudentId && p.AssignmentId == attempt.AssignmentId, cancellationToken);
                if (progress is not null)
                {
                    progress.IsOverallAiCommentStale = true;
                    progress.TeacherFinalReviewStatus = EduTwin.Contracts.Assignments.TeacherFinalReviewStatus.Pending;
                    progress.FinalReviewedByUserId = null;
                    progress.FinalReviewedAt = null;
                    progress.FinalTeacherNote = null;
                    progress.FinalReviewVersion++;
                    progress.RowVersion++;
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        foreach (var studentId in changes.Select(a => a.StudentId).Distinct())
            await ReplayAsync(centerId, studentId, question.SubjectId, question.PrimaryTopicNodeId, changes.First(a => a.StudentId == studentId), now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return changes.Count;
    }

    private async Task ReplayAsync(Guid center, Guid student, Guid subject, ulong topic, Attempt trigger, DateTime now, CancellationToken ct)
    {
        var samples = await new BehaviorCalibrationSampleProvider(db).GetSubjectSamplesAsync(center, student, subject, trigger, false, null, ct);
        var calibration = new BehaviorCalibrationCalculator();
        var all = await db.Attempts.Include(a => a.Question).Where(a => a.CenterId == center && a.StudentId == student && a.Question.SubjectId == subject)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.AttemptId).ToListAsync(ct);
        var analyses = await (from r in db.ReasoningAnalyses join a in db.Attempts on new { r.CenterId, r.AttemptId } equals new { a.CenterId, a.AttemptId }
                              where a.CenterId == center && a.StudentId == student && a.Question.SubjectId == subject select r).ToDictionaryAsync(r => r.AttemptId, ct);
        var evidenceRows = await (from e in db.EvidenceAssessments join a in db.Attempts on new { e.CenterId, e.AttemptId } equals new { a.CenterId, a.AttemptId }
                                  where a.CenterId == center && a.StudentId == student && a.Question.SubjectId == subject select e).ToListAsync(ct);
        var evidences = evidenceRows.GroupBy(e => e.AttemptId).ToDictionary(g => g.Key, g => g.OrderBy(e => e.EvaluatedAt).ThenBy(e => e.EvidenceAssessmentId).Last());
        var twin = await db.KnowledgeTwins.SingleOrDefaultAsync(t => t.CenterId == center && t.StudentId == student && t.SubjectId == subject && t.TopicNodeId == topic && !t.IsDeleted, ct);
        if (twin is null) throw new InvalidOperationException("Existing grading evidence has no matching knowledge twin.");
        var previous = twin.MasteryPercentage;
        decimal mastery = 0m;
        uint count = 0;
        var steps = new List<ReplayStepBreakdown>();
        decimal? latestQuality = null;
        ulong? latestAttempt = null;
        foreach (var a in all.Where(a => a.Question.PrimaryTopicNodeId == topic))
        {
            var r = analyses.GetValueOrDefault(a.AttemptId);
            var correct = r?.OverrideIsCorrect ?? a.IsCorrect;
            var quality = r?.OverrideReasoningQuality ?? r?.ReasoningQuality;
            var weight = evidences.GetValueOrDefault(a.AttemptId)?.ReasoningWeight ?? 0m;
            var rolling = samples.Where(s => s.EffectiveIsCorrect.HasValue && (s.CreatedAt < a.CreatedAt || s.CreatedAt == a.CreatedAt && s.AttemptId <= a.AttemptId))
                .Select(s => new GradedAttemptSample(s.Confidence, s.EffectiveIsCorrect!.Value));
            var calibrated = calibration.CalculateCalibration(rolling);
            var ratio = a.Question.EstimatedTimeSeconds > 0 ? a.TimeSpentSeconds / (decimal)a.Question.EstimatedTimeSeconds : 1m;
            var time = Math.Round(Math.Clamp(1m - Math.Abs(ratio - 1m) * .5m, 0m, 1m), 2, MidpointRounding.AwayFromZero);
            var calc = MasteryCalculator.Calculate(new(mastery, quality, weight, correct, time, weight > 0m ? calibrated / 100m : null, a.Question.Difficulty));
            steps.Add(new(a.AttemptId, quality, weight, correct, time, calibrated, a.Question.Difficulty, calc.Breakdown.DifficultyMultiplier,
                calc.Breakdown.LearningRate, mastery, calc.NewMastery, calc.Delta));
            mastery = calc.NewMastery;
            if (weight > 0m) { count++; latestQuality = quality; latestAttempt = a.AttemptId; }
        }
        twin.MasteryPercentage = mastery;
        twin.EvidenceCount = count;
        twin.LastReasoningQuality = latestQuality;
        twin.LastAttemptId = latestAttempt;
        twin.UpdatedAt = now;
        var finalCalibration = calibration.CalculateCalibration(samples.Where(s => s.EffectiveIsCorrect.HasValue).Select(s => new GradedAttemptSample(s.Confidence, s.EffectiveIsCorrect!.Value)));
        var behavior = await db.BehaviorTwins.SingleOrDefaultAsync(t => t.CenterId == center && t.StudentId == student && t.SubjectId == subject && !t.IsDeleted, ct);
        if (behavior is not null) { behavior.ConfidenceCalibration = finalCalibration; behavior.UpdatedAt = now; }
        await new StudentGoalRiskUpdater(db).UpdateAsync(center, student, subject, now, ct);
        await new StudentTwinUpdater(db).UpdateAsync(center, student, now, ct);
        var summary = new ReplaySummaryBreakdown(trigger.AttemptId, previous, mastery, steps.Count, (int)count, finalCalibration, steps);
        db.TwinUpdateHistories.Add(new()
        {
            CenterId = center, StudentId = student, SubjectId = subject, TopicNodeId = topic, AttemptId = trigger.AttemptId,
            AnalysisId = analyses.GetValueOrDefault(trigger.AttemptId)?.AnalysisId, EventSource = TwinEventSource.Replay,
            PreviousMastery = previous, NewMastery = mastery, MasteryDelta = mastery - previous, EffectiveReasoningQuality = latestQuality,
            CalculationVersion = "math-replay-v1", CalculationBreakdown = JsonSerializer.SerializeToDocument(summary),
            Explanation = "Replayed after bounded mathematical canonicalization; previous evidence retained, corrected legacy AI evidence awaits teacher review.", CreatedAt = now
        });
    }
}
