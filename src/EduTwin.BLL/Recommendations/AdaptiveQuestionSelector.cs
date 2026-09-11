using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations;

public interface IAdaptiveQuestionSelector
{
    Task<Question?> SelectQuestionAsync(
        Guid centerId,
        Guid studentId,
        ulong topicNodeId,
        decimal currentMastery,
        CancellationToken cancellationToken);
}

public sealed class AdaptiveQuestionSelector : IAdaptiveQuestionSelector
{
    private readonly EduTwinDbContext _dbContext;

    public AdaptiveQuestionSelector(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public static byte CalculateTargetDifficulty(decimal mastery)
    {
        if (mastery < 20m) return 1;
        if (mastery < 40m) return 2;
        if (mastery < 60m) return 3;
        if (mastery < 80m) return 4;
        return 5;
    }

    public async Task<Question?> SelectQuestionAsync(
        Guid centerId,
        Guid studentId,
        ulong topicNodeId,
        decimal currentMastery,
        CancellationToken cancellationToken)
    {
        // 1. Fetch active questions for this topic
        var activeQuestions = await _dbContext.Questions
            .Where(q => q.CenterId == centerId
                && q.PrimaryTopicNodeId == topicNodeId
                && q.Status == QuestionStatus.Active
                && !q.IsDeleted)
            .ToListAsync(cancellationToken);

        if (activeQuestions.Count == 0)
        {
            return null;
        }

        // 2. Fetch all student's attempts in this topic to identify attempt history and recency
        var studentAttemptsInTopic = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId
                && a.StudentId == studentId
                && a.Question.PrimaryTopicNodeId == topicNodeId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AttemptId)
            .Select(a => new { a.QuestionId, a.CreatedAt })
            .ToListAsync(cancellationToken);

        var recentAttemptedQuestionIds = new HashSet<ulong>(
            studentAttemptsInTopic.Take(3).Select(a => a.QuestionId));

        var lastAttemptTimeByQuestion = studentAttemptsInTopic
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CreatedAt));

        byte targetDifficulty = CalculateTargetDifficulty(currentMastery);

        // 3. Pool 1 (unattempted) and Pool 2 (attempted, but not in last 3 attempts)
        var nonRecentQuestions = activeQuestions
            .Where(q => !recentAttemptedQuestionIds.Contains(q.QuestionId))
            .ToList();

        if (nonRecentQuestions.Count > 0)
        {
            // Pick from non-recent pool:
            // Unattempted first -> Difficulty distance ASC -> Least recently attempted ASC -> QuestionId ASC
            return nonRecentQuestions
                .OrderBy(q => lastAttemptTimeByQuestion.ContainsKey(q.QuestionId) ? 1 : 0)
                .ThenBy(q => Math.Abs(q.Difficulty - targetDifficulty))
                .ThenBy(q => lastAttemptTimeByQuestion.GetValueOrDefault(q.QuestionId, DateTime.MinValue))
                .ThenBy(q => q.QuestionId)
                .First();
        }

        // 4. All active questions were attempted recently: relax recency constraint (LRU)
        // Least recently attempted ASC -> Difficulty distance ASC -> QuestionId ASC
        return activeQuestions
            .OrderBy(q => lastAttemptTimeByQuestion.GetValueOrDefault(q.QuestionId, DateTime.MinValue))
            .ThenBy(q => Math.Abs(q.Difficulty - targetDifficulty))
            .ThenBy(q => q.QuestionId)
            .First();
    }
}
