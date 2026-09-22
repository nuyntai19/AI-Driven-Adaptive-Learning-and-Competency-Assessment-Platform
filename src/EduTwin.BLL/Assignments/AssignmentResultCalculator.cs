using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Assignments;

public interface IAssignmentResultCalculator
{
    Task<AssignmentResultSummaryDto> CalculateForSingleAssignmentAsync(
        Guid centerId,
        Guid studentId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<Dictionary<Guid, AssignmentResultSummaryDto>> CalculateBatchForAssignmentsAsync(
        Guid centerId,
        Guid studentId,
        IReadOnlyCollection<Guid> assignmentIds,
        CancellationToken cancellationToken);
}

public sealed class AssignmentResultCalculator : IAssignmentResultCalculator
{
    private readonly EduTwinDbContext _dbContext;

    public AssignmentResultCalculator(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AssignmentResultSummaryDto> CalculateForSingleAssignmentAsync(
        Guid centerId,
        Guid studentId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var map = await CalculateBatchForAssignmentsAsync(centerId, studentId, new[] { assignmentId }, cancellationToken);
        return map.TryGetValue(assignmentId, out var summary) ? summary : CreateEmptySummary(0);
    }

    public async Task<Dictionary<Guid, AssignmentResultSummaryDto>> CalculateBatchForAssignmentsAsync(
        Guid centerId,
        Guid studentId,
        IReadOnlyCollection<Guid> assignmentIds,
        CancellationToken cancellationToken)
    {
        if (assignmentIds.Count == 0)
        {
            return new Dictionary<Guid, AssignmentResultSummaryDto>();
        }

        // 1. Batch load assignment questions with question max scores
        var questionsQuery = _dbContext.AssignmentQuestions
            .AsNoTracking()
            .Where(aq => aq.CenterId == centerId);

        var assignmentQuestions = await WhereIn(questionsQuery, aq => aq.AssignmentId, assignmentIds)
            .Select(aq => new
            {
                aq.AssignmentId,
                aq.QuestionId,
                MaxScore = aq.Question != null && aq.Question.MaxScore > 0
                    ? aq.Question.MaxScore
                    : aq.Points > 0 ? aq.Points : 1.00m
            })
            .ToListAsync(cancellationToken);

        var questionsByAssignment = assignmentQuestions
            .GroupBy(aq => aq.AssignmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 2. Batch load attempts for these assignments and student
        var attemptsQuery = _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.CenterId == centerId &&
                        a.StudentId == studentId &&
                        a.AssignmentId.HasValue);

        var nullableAssignmentIds = assignmentIds.Select(id => (Guid?)id).ToList();
        var allAttempts = await WhereIn(attemptsQuery, a => a.AssignmentId, nullableAssignmentIds)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        // Group to get latest attempt per question per assignment
        var latestAttemptsByQuestion = allAttempts
            .GroupBy(a => new { a.AssignmentId!.Value, a.QuestionId })
            .ToDictionary(g => g.Key, g => g.First());

        var latestAttemptIds = latestAttemptsByQuestion.Values.Select(a => a.AttemptId).Distinct().ToList();

        // 3. Batch load ReasoningAnalyses for these latest attempts
        List<ReasoningAnalysis> analyses;
        if (latestAttemptIds.Count > 0)
        {
            var analysesQuery = _dbContext.ReasoningAnalyses
                .AsNoTracking()
                .Where(ra => ra.CenterId == centerId);

            analyses = await WhereIn(analysesQuery, ra => ra.AttemptId, latestAttemptIds)
                .ToListAsync(cancellationToken);
        }
        else
        {
            analyses = new List<ReasoningAnalysis>();
        }

        var analysesByAttemptId = analyses
            .GroupBy(ra => ra.AttemptId)
            .ToDictionary(g => g.Key, g => g.First());

        // 4. Batch load StudentAssignmentProgresses for cached overall comments
        var progressesQuery = _dbContext.StudentAssignmentProgresses
            .AsNoTracking()
            .Where(p => p.CenterId == centerId &&
                        p.StudentId == studentId &&
                        !p.IsDeleted);

        var progresses = await WhereIn(progressesQuery, p => p.AssignmentId, assignmentIds)
            .ToListAsync(cancellationToken);

        var progressesByAssignment = progresses.ToDictionary(p => p.AssignmentId);

        var reviewerIds = progresses
            .Where(p => p.FinalReviewedByUserId.HasValue)
            .Select(p => p.FinalReviewedByUserId!.Value)
            .Distinct()
            .ToList();
        var reviewerNames = reviewerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.CenterId == centerId && reviewerIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.DisplayName, cancellationToken);

        // 5. Compute summary deterministically for each assignment
        var resultMap = new Dictionary<Guid, AssignmentResultSummaryDto>(assignmentIds.Count);

        foreach (var assignmentId in assignmentIds)
        {
            var questions = questionsByAssignment.GetValueOrDefault(assignmentId) ?? new();
            var totalQuestionCount = questions.Count;
            const decimal assignmentMaxScore = 10m;
            var scorePerQuestion = totalQuestionCount > 0 ? assignmentMaxScore / totalQuestionCount : 0m;

            decimal totalAwardedScore = 0m;
            int answeredQuestionCount = 0;
            int evaluatedQuestionCount = 0;
            int correctQuestionCount = 0;

            foreach (var q in questions)
            {
                var hasAttempt = latestAttemptsByQuestion.TryGetValue(new { Value = assignmentId, q.QuestionId }, out var attempt);
                if (!hasAttempt || attempt == null)
                {
                    continue;
                }

                answeredQuestionCount++;

                var analysis = analysesByAttemptId.GetValueOrDefault(attempt.AttemptId);

                // Effective grade calculation
                decimal? effectiveScore = analysis?.OverrideAwardedScore ?? attempt.AwardedScore;
                bool? effectiveIsCorrect = analysis?.OverrideIsCorrect ?? attempt.IsCorrect;

                if (effectiveScore.HasValue || effectiveIsCorrect.HasValue)
                {
                    var earnedRatio = effectiveScore.HasValue && q.MaxScore > 0
                        ? Math.Clamp(effectiveScore.Value / q.MaxScore, 0m, 1m)
                        : effectiveIsCorrect == true ? 1m : 0m;
                    totalAwardedScore += scorePerQuestion * earnedRatio;
                    evaluatedQuestionCount++;

                    if (effectiveIsCorrect == true)
                    {
                        correctQuestionCount++;
                    }
                }
            }

            // Read cached overall AI comment (never calls LLM here)
            var progress = progressesByAssignment.GetValueOrDefault(assignmentId);
            var finalReviewStatus = progress?.TeacherFinalReviewStatus ?? TeacherFinalReviewStatus.Pending;
            var pendingQuestionCount = Math.Max(0, totalQuestionCount - evaluatedQuestionCount);
            var resultStatus = finalReviewStatus == TeacherFinalReviewStatus.Approved
                ? "Final"
                : evaluatedQuestionCount > 0 && pendingQuestionCount == 0
                    ? "Provisional"
                    : "Processing";
            string? cachedComment = null;
            DateTime? commentGeneratedAt = null;

            if (progress != null && !progress.IsOverallAiCommentStale && !string.IsNullOrWhiteSpace(progress.OverallAiComment))
            {
                cachedComment = progress.OverallAiComment;
                commentGeneratedAt = progress.OverallAiCommentGeneratedAt;
            }

            resultMap[assignmentId] = new AssignmentResultSummaryDto
            {
                TotalQuestionCount = totalQuestionCount,
                AnsweredQuestionCount = answeredQuestionCount,
                EvaluatedQuestionCount = evaluatedQuestionCount,
                CorrectQuestionCount = correctQuestionCount,
                IncorrectQuestionCount = Math.Max(0, evaluatedQuestionCount - correctQuestionCount),
                PendingQuestionCount = pendingQuestionCount,
                ResultStatus = resultStatus,
                TeacherFinalReviewStatus = finalReviewStatus.ToString(),
                InternalAwardedScore = evaluatedQuestionCount > 0 ? decimal.Round(totalAwardedScore, 2) : null,
                InternalMaxScore = totalQuestionCount > 0 ? assignmentMaxScore : 0m,
                OverallAiComment = cachedComment,
                OverallAiCommentGeneratedAt = commentGeneratedAt,
                FinalTeacherNote = progress?.FinalTeacherNote,
                FinalReviewedByName = progress?.FinalReviewedByUserId is { } reviewerId
                    ? reviewerNames.GetValueOrDefault(reviewerId)
                    : null,
                FinalReviewedAt = progress?.FinalReviewedAt
            };
        }

        return resultMap;
    }

    private static AssignmentResultSummaryDto CreateEmptySummary(decimal maxScore) => new()
    {
        TotalQuestionCount = 0,
        AnsweredQuestionCount = 0,
        EvaluatedQuestionCount = 0,
        CorrectQuestionCount = 0,
        IncorrectQuestionCount = 0,
        PendingQuestionCount = 0,
        ResultStatus = "Processing",
        TeacherFinalReviewStatus = "Pending",
        InternalAwardedScore = null,
        InternalMaxScore = maxScore,
        OverallAiComment = null,
        OverallAiCommentGeneratedAt = null,
        FinalTeacherNote = null,
        FinalReviewedByName = null,
        FinalReviewedAt = null
    };

    private static IQueryable<T> WhereIn<T, TKey>(
        IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        IEnumerable<TKey> keys)
    {
        var keyList = keys as IReadOnlyCollection<TKey> ?? keys.ToList();
        if (keyList.Count == 0)
        {
            return query.Where(_ => false);
        }

        var parameter = keySelector.Parameters[0];
        var propertyAccess = keySelector.Body;

        Expression? body = null;
        foreach (var key in keyList)
        {
            var constant = Expression.Constant(key, typeof(TKey));
            var equal = Expression.Equal(propertyAccess, constant);
            body = body == null ? equal : Expression.OrElse(body, equal);
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(body!, parameter));
    }
}
