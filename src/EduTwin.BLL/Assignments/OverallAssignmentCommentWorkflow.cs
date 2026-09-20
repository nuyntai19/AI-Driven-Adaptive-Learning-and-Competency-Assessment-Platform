using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Assignments;

public interface IOverallAssignmentCommentWorkflow
{
    Task InvalidateCommentAsync(
        Guid centerId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken);

    Task<string?> GenerateAndCacheOverallCommentAsync(
        Guid centerId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken);
}

public sealed class OverallAssignmentCommentWorkflow : IOverallAssignmentCommentWorkflow
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public OverallAssignmentCommentWorkflow(
        EduTwinDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task InvalidateCommentAsync(
        Guid centerId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var progress = await _dbContext.StudentAssignmentProgresses
            .FirstOrDefaultAsync(p => p.CenterId == centerId &&
                                      p.AssignmentId == assignmentId &&
                                      p.StudentId == studentId &&
                                      !p.IsDeleted, cancellationToken);

        if (progress != null)
        {
            progress.IsOverallAiCommentStale = true;
            progress.OverallAiComment = null;
            progress.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<string?> GenerateAndCacheOverallCommentAsync(
        Guid centerId,
        Guid assignmentId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var progress = await _dbContext.StudentAssignmentProgresses
            .FirstOrDefaultAsync(p => p.CenterId == centerId &&
                                      p.AssignmentId == assignmentId &&
                                      p.StudentId == studentId &&
                                      !p.IsDeleted, cancellationToken);

        if (progress == null)
        {
            return null;
        }

        // Return cache if still valid
        if (!progress.IsOverallAiCommentStale && !string.IsNullOrWhiteSpace(progress.OverallAiComment))
        {
            return progress.OverallAiComment;
        }

        var assignmentQuestions = await _dbContext.AssignmentQuestions
            .AsNoTracking()
            .Where(aq => aq.CenterId == centerId && aq.AssignmentId == assignmentId)
            .OrderBy(aq => aq.OrderIndex)
            .Select(aq => new { aq.QuestionId, aq.Question!.QuestionText, aq.Question.MaxScore, aq.OrderIndex })
            .ToListAsync(cancellationToken);

        if (assignmentQuestions.Count == 0)
        {
            return null;
        }

        var questionIds = assignmentQuestions.Select(q => q.QuestionId).ToList();

        var attempts = await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.CenterId == centerId && a.StudentId == studentId && a.AssignmentId == assignmentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestAttempts = attempts
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.First());

        var attemptIds = latestAttempts.Values.Select(a => a.AttemptId).ToList();
        Dictionary<ulong, EduTwin.DAL.AssessmentAndReasoning.ReasoningAnalysis> analyses;
        if (attemptIds.Count > 0)
        {
            var analysesQuery = _dbContext.ReasoningAnalyses
                .AsNoTracking()
                .Where(ra => ra.CenterId == centerId);

            analyses = await WhereIn(analysesQuery, ra => ra.AttemptId, attemptIds)
                .ToDictionaryAsync(ra => ra.AttemptId, cancellationToken);
        }
        else
        {
            analyses = new Dictionary<ulong, EduTwin.DAL.AssessmentAndReasoning.ReasoningAnalysis>();
        }

        // Check if any question is still processing
        var hasActiveProcessing = latestAttempts.Values.Any(a => a.Status == AttemptStatus.PendingAnalysis || a.Status == AttemptStatus.Processing);
        if (hasActiveProcessing)
        {
            return null; // Not all questions reached terminal state
        }

        // Synthesize overall commentary across the entire assignment
        var totalQuestions = assignmentQuestions.Count;
        int correctCount = 0;
        int partialOrIncorrectCount = 0;
        var errorTypes = new List<string>();
        var misconceptions = new List<string>();

        foreach (var q in assignmentQuestions)
        {
            if (latestAttempts.TryGetValue(q.QuestionId, out var att))
            {
                var analysis = analyses.GetValueOrDefault(att.AttemptId);
                var isCorrect = analysis?.OverrideIsCorrect ?? att.IsCorrect;

                if (isCorrect == true)
                {
                    correctCount++;
                }
                else
                {
                    partialOrIncorrectCount++;
                }

                if (analysis != null)
                {
                    var err = analysis.OverrideErrorType ?? analysis.ErrorType;
                    if (err != ErrorType.None)
                    {
                        errorTypes.Add(err.ToString());
                    }

                    if (!string.IsNullOrWhiteSpace(analysis.Misconception))
                    {
                        misconceptions.Add(analysis.Misconception);
                    }
                }
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var comment = BuildSynthesis(totalQuestions, correctCount, partialOrIncorrectCount, errorTypes, misconceptions);

        progress.OverallAiComment = comment;
        progress.OverallAiCommentGeneratedAt = now;
        progress.OverallAiCommentVersion++;
        progress.IsOverallAiCommentStale = false;
        progress.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return comment;
    }

    private static string BuildSynthesis(
        int totalQuestions,
        int correctCount,
        int partialOrIncorrectCount,
        List<string> errorTypes,
        List<string> misconceptions)
    {
        var parts = new List<string>();

        if (correctCount == totalQuestions)
        {
            parts.Add($"Xuất sắc! Bạn đã hoàn thành chính xác toàn bộ {totalQuestions} câu hỏi của bài tập với lập luận mạch lạc và chuẩn xác.");
            parts.Add("Điểm mạnh chung: Nắm vững các định lý cơ bản, trình bày tư duy rõ ràng và tính toán chính xác.");
            parts.Add("Gợi ý phát triển: Bạn có thể thử thách bản thân với các dạng bài vận dụng cao trong mục Luyện tập thích ứng để mở rộng năng lực.");
        }
        else
        {
            parts.Add($"Bạn đã hoàn thành {correctCount}/{totalQuestions} câu hỏi chính xác.");

            if (correctCount > 0)
            {
                parts.Add("Điểm mạnh chung: Bạn thể hiện tốt khả năng hiểu đề bài và thiết lập các bước biến đổi ban đầu.");
            }

            if (partialOrIncorrectCount > 0)
            {
                var distinctErrors = errorTypes.Distinct().Take(2).ToList();
                if (distinctErrors.Count > 0)
                {
                    var errNames = string.Join(", ", distinctErrors.Select(MapErrorName));
                    parts.Add($"Lỗi lặp lại đáng chú ý: Hệ thống phát hiện các lỗi tập trung vào nhóm ({errNames}).");
                }

                if (misconceptions.Count > 0)
                {
                    var sampleMisconception = misconceptions.First();
                    parts.Add($"Khái niệm cần củng cố: Lưu ý về \"{sampleMisconception}\".");
                }

                parts.Add("Gợi ý cải thiện: Nên rà soát lại các bước điều kiện xác định và phép tính trung gian trước khi đưa ra kết luận cuối cùng.");
            }
        }

        return string.Join(" ", parts);
    }

    private static string MapErrorName(string err) => err switch
    {
        "Knowledge" => "hổng kiến thức nền",
        "Skill" => "kỹ năng tính toán biến đổi",
        "Reasoning" => "lập luận logic",
        "Behavior" => "bất cẩn / thao tác",
        "Presentation" => "trình bày ký hiệu",
        _ => "phương pháp giải"
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
