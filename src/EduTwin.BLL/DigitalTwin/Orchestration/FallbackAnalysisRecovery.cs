using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.DigitalTwin.Orchestration;

// A provider recovery replaces only an unreviewed rule-based placeholder, not a
// student's submission or a teacher decision. Its old payload is retained in the
// append-only history and its evidence is superseded, never deleted or updated.
internal static class FallbackAnalysisRecovery
{
    // twin_update_history.calculation_version is varchar(20). Keep the replay
    // identifier stable and within that contract; the full context is in history.
    private const string CalculationVersion = "fallback-replay-v1";

    public static object Replace(ReasoningAnalysis existing, ReasoningAnalysis next, DateTime now)
    {
        var snapshot = new
        {
            existing.AnalysisId, existing.SchemaVersion, existing.MethodDetected,
            existing.ReasoningQuality, existing.ErrorType, existing.Misconception,
            MissingSteps = existing.MissingSteps.RootElement.Clone(),
            RootCauseNodeIds = existing.RootCauseNodeIds.RootElement.Clone(),
            existing.AnalysisConfidence, existing.Feedback, existing.IsFallback,
            existing.NeedsTeacherReview, existing.Provider, existing.ModelName, existing.AnalysisProfileVersion,
            existing.SolutionType, existing.AiSolution, existing.AnswerAssessment,
            existing.ReasoningVerdict, existing.FeedbackOrigin, existing.CreatedAt, existing.UpdatedAt,
            existing.SuggestedScore, existing.SuggestedRubricGradeJson, existing.UsesAlternativeMethod
        };
        existing.SchemaVersion = next.SchemaVersion;
        existing.MethodDetected = next.MethodDetected;
        existing.ReasoningQuality = next.ReasoningQuality;
        existing.ErrorType = next.ErrorType;
        existing.Misconception = next.Misconception;
        existing.MissingSteps = next.MissingSteps;
        existing.RootCauseNodeIds = next.RootCauseNodeIds;
        existing.AnalysisConfidence = next.AnalysisConfidence;
        existing.Feedback = next.Feedback;
        existing.IsFallback = next.IsFallback;
        existing.NeedsTeacherReview = next.NeedsTeacherReview;
        existing.Provider = next.Provider;
        existing.ModelName = next.ModelName;
        existing.AnalysisProfileVersion = next.AnalysisProfileVersion;
        existing.SolutionType = next.SolutionType;
        existing.AiSolution = next.AiSolution;
        existing.AnswerAssessment = next.AnswerAssessment;
        existing.ReasoningVerdict = next.ReasoningVerdict;
        existing.SuggestedScore = next.SuggestedScore;
        existing.SuggestedRubricGradeJson = next.SuggestedRubricGradeJson;
        existing.UsesAlternativeMethod = next.UsesAlternativeMethod;
        existing.FeedbackOrigin = next.FeedbackOrigin;
        existing.UpdatedAt = now;
        return snapshot;
    }

    public static async Task<KnowledgeTwinUpdateResult> ReplayAsync(EduTwinDbContext db,
        Attempt trigger, Question question, EvidenceAssessment successor, DateTime now,
        CancellationToken cancellationToken)
    {
        var items = await (from a in db.Attempts
            join q in db.Questions on new { a.CenterId, a.QuestionId } equals new { q.CenterId, q.QuestionId }
            where a.CenterId == trigger.CenterId && a.StudentId == trigger.StudentId &&
                q.SubjectId == question.SubjectId && q.PrimaryTopicNodeId == question.PrimaryTopicNodeId
            orderby a.CreatedAt, a.AttemptId
            select new { Attempt = a, Question = q }).ToListAsync(cancellationToken);
        var ids = items.Select(i => i.Attempt.AttemptId).ToArray();
        var analyses = (await db.ReasoningAnalyses.Where(a => a.CenterId == trigger.CenterId &&
            ids.Contains(a.AttemptId)).ToListAsync(cancellationToken)).ToDictionary(a => a.AttemptId);
        var evidence = (await db.EvidenceAssessments.Where(e => e.CenterId == trigger.CenterId &&
            ids.Contains(e.AttemptId)).OrderBy(e => e.EvaluatedAt).ThenBy(e => e.EvidenceAssessmentId)
            .ToListAsync(cancellationToken)).GroupBy(e => e.AttemptId).ToDictionary(g => g.Key, g => g.Last());
        evidence[trigger.AttemptId] = successor;
        var samples = await new BehaviorCalibrationSampleProvider(db).GetSubjectSamplesAsync(
            trigger.CenterId, trigger.StudentId, question.SubjectId, trigger, false, null, cancellationToken);
        var calculator = new BehaviorCalibrationCalculator();
        var steps = new List<ReplayStepBreakdown>();
        decimal mastery = 0m;
        uint count = 0;
        decimal? lastQuality = null;
        ulong? lastAttempt = null;
        foreach (var item in items)
        {
            var a = item.Attempt;
            var q = item.Question;
            var analysis = analyses.GetValueOrDefault(a.AttemptId);
            var weight = evidence.GetValueOrDefault(a.AttemptId)?.ReasoningWeight ?? 0m;
            var correctness = analysis?.OverrideIsCorrect ?? a.IsCorrect;
            decimal? quality = analysis is null ? null : analysis.OverrideReasoningQuality ??
                analysis.ReasoningQuality ?? (correctness == true ? 70m : 30m);
            var calibration = calculator.CalculateCalibration(samples.Where(s => s.EffectiveIsCorrect.HasValue &&
                (s.CreatedAt < a.CreatedAt || s.CreatedAt == a.CreatedAt && s.AttemptId <= a.AttemptId))
                .Select(s => new GradedAttemptSample(s.Confidence, s.EffectiveIsCorrect!.Value)));
            var ratio = q.EstimatedTimeSeconds > 0 ? a.TimeSpentSeconds / (decimal)q.EstimatedTimeSeconds : 1m;
            var timeQuality = Math.Round(Math.Clamp(1m - Math.Abs(ratio - 1m) * 0.5m, 0m, 1m), 2);
            var calc = MasteryCalculator.Calculate(new MasteryCalculationInput(mastery, quality, weight,
                correctness, timeQuality, weight > 0 && quality.HasValue ? calibration / 100m : null, q.Difficulty));
            steps.Add(new ReplayStepBreakdown(a.AttemptId, quality, weight, correctness, timeQuality, calibration,
                q.Difficulty, calc.Breakdown.DifficultyMultiplier, calc.Breakdown.LearningRate,
                mastery, calc.NewMastery, calc.Delta));
            mastery = calc.NewMastery;
            if (weight > 0) { count++; lastQuality = quality; lastAttempt = a.AttemptId; }
        }
        var twin = await db.KnowledgeTwins.SingleOrDefaultAsync(k => k.CenterId == trigger.CenterId &&
            k.StudentId == trigger.StudentId && k.SubjectId == question.SubjectId &&
            k.TopicNodeId == question.PrimaryTopicNodeId && !k.IsDeleted, cancellationToken);
        var previous = twin?.MasteryPercentage ?? 0m;
        if (twin is null)
        {
            twin = new KnowledgeTwin { KnowledgeTwinId = TwinAggregateIdGenerator.NewId(), CenterId = trigger.CenterId,
                StudentId = trigger.StudentId, SubjectId = question.SubjectId, TopicNodeId = question.PrimaryTopicNodeId,
                CreatedAt = now };
            db.KnowledgeTwins.Add(twin);
        }
        twin.MasteryPercentage = mastery;
        twin.EvidenceCount = count;
        twin.LastReasoningQuality = lastQuality;
        twin.LastAttemptId = lastAttempt;
        twin.LastEvidenceAt = now;
        twin.UpdatedAt = now;
        var last = steps.LastOrDefault();
        var breakdown = new MasteryCalculationBreakdown(false, previous, lastQuality / 100m,
            successor.ReasoningWeight, trigger.IsCorrect.HasValue ? trigger.IsCorrect.Value ? 1m : 0m : null,
            last?.TimeQuality ?? 0.5m, last?.RollingCalibration / 100m, question.Difficulty,
            last?.DifficultyMultiplier ?? 1m, last?.LearningRate ?? 0m, lastQuality ?? 0m,
            mastery, mastery, mastery - previous, steps);
        return new KnowledgeTwinUpdateResult(twin, new MasteryCalculationResult(previous, mastery, mastery - previous,
            lastQuality, CalculationVersion, breakdown,
            "AI recovered after a provider failure; existing attempts replayed once, not duplicated."));
    }
}
