using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations.UseCases;

public sealed record GetNextQuestionResult
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public bool Forbidden { get; init; }
    public string? ErrorMessage { get; init; }
    public NextQuestionDto? Data { get; init; }

    public static GetNextQuestionResult Success(NextQuestionDto data) =>
        new() { IsSuccess = true, Data = data };

    public static GetNextQuestionResult FailNotFound(string message = "Resource not found.") =>
        new() { NotFound = true, ErrorMessage = message };

    public static GetNextQuestionResult FailForbidden(string message = "Forbidden.") =>
        new() { Forbidden = true, ErrorMessage = message };
}

public interface IGetNextQuestionUseCase
{
    Task<GetNextQuestionResult> ExecuteSessionAsync(Guid subjectId, string sessionId, Guid? classId, bool history, CancellationToken ct)
        => Task.FromResult(GetNextQuestionResult.FailNotFound("Buổi học không tồn tại."));
    Task<GetNextQuestionResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
    Task<GetNextQuestionResult> ExecuteAsync(Guid subjectId, Guid? classId, bool history, CancellationToken ct)
        => history || classId.HasValue ? Task.FromResult(GetNextQuestionResult.FailForbidden("Phạm vi học tập không được hỗ trợ.")) : ExecuteAsync(subjectId, ct);
}

public sealed class GetNextQuestionUseCase : IGetNextQuestionUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly TimeProvider _timeProvider;

    public GetNextQuestionUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IRecommendationEngine recommendationEngine,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _recommendationEngine = recommendationEngine ?? throw new ArgumentNullException(nameof(recommendationEngine));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GetNextQuestionResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
        => await ExecuteAsync(subjectId, null, false, cancellationToken);

    public async Task<GetNextQuestionResult> ExecuteSessionAsync(Guid subjectId, string sessionId, Guid? classId, bool history, CancellationToken ct)
    {
        var accessible = await LearningPathSessionAccess.LoadAsync(_dbContext, _tenantContext, subjectId, sessionId, classId, history, ct);
        if (accessible is null || accessible.Session.Status != "InProgress" || accessible.Session.StartedAtUtc is null)
            return GetNextQuestionResult.FailNotFound("Buổi học chưa bắt đầu, đã hoàn thành hoặc không thuộc phạm vi đang học.");
        var (path, _, session) = accessible;
        var ids = session.RecommendedQuestionIds.Select(ulong.Parse).Distinct().ToList();
        var attempts = await _dbContext.Attempts.AsNoTracking().Where(a => a.CenterId == path.CenterId && a.StudentId == path.StudentId &&
            a.AssignmentId == null && ids.Contains(a.QuestionId) && a.CreatedAt >= session.StartedAtUtc && !a.Skipped)
            .Select(a => a.QuestionId).ToListAsync(ct);
        var remaining = ids.Where(id => !attempts.Contains(id)).ToList();
        var topicId = ulong.Parse(session.TopicNodeId);
        var questions = await _dbContext.Questions.AsNoTracking().Where(q => q.CenterId == path.CenterId && q.SubjectId == subjectId &&
            q.PrimaryTopicNodeId == topicId && remaining.Contains(q.QuestionId) && q.Status == QuestionStatus.Active && !q.IsDeleted).ToListAsync(ct);
        var question = remaining.Select(id => questions.FirstOrDefault(q => q.QuestionId == id)).FirstOrDefault(q => q is not null);
        if (question is null) return GetNextQuestionResult.FailNotFound("Buổi học đã làm hết câu hoặc câu hỏi không còn khả dụng. Vui lòng quay lại lộ trình.");
        var topic = await _dbContext.KnowledgeNodes.AsNoTracking().SingleOrDefaultAsync(n => n.CenterId == path.CenterId && n.NodeId == topicId && n.IsActive && !n.IsDeleted, ct);
        if (topic is null) return GetNextQuestionResult.FailNotFound("Chủ đề không còn khả dụng.");
        var options = await _dbContext.QuestionOptions.AsNoTracking().Where(o => o.CenterId == path.CenterId && o.QuestionId == question.QuestionId && !o.IsDeleted)
            .OrderBy(o => o.OrderIndex).Select(o => new StudentQuestionOptionDto { OptionId = o.OptionId.ToString(), Label = o.OptionLabel, Text = o.OptionText, OrderIndex = o.OrderIndex }).ToListAsync(ct);
        return GetNextQuestionResult.Success(new NextQuestionDto {
            Strategy = path.Strategy, Topic = new(topicId, topic.NodeName, 0), Explanation = session.Objective,
            Question = new(question.QuestionId, question.QuestionType, question.Difficulty, question.QuestionText, question.MaxScore,
                question.EstimatedTimeSeconds, question.ReasoningRequired, question.LanguageCode, options, question.AnswerEvaluationMode, question.HasImage)
        });
    }

    public async Task<GetNextQuestionResult> ExecuteAsync(Guid subjectId, Guid? classId, bool history, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            return GetNextQuestionResult.FailForbidden("Tenant context is not resolved.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        var studentExists = await _dbContext.Students
            .AnyAsync(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted, cancellationToken);
        if (!studentExists)
        {
            return GetNextQuestionResult.FailNotFound("Student record not found in center.");
        }

        var subjectExists = await _dbContext.Subjects
            .AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId && !s.IsDeleted, cancellationToken);
        if (!subjectExists)
        {
            return GetNextQuestionResult.FailNotFound("Subject not found in center.");
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var scope = await EduTwin.BLL.Organization.StudentLearningScope.ResolveAsync(_dbContext, centerId, studentId, subjectId, classId, history, cancellationToken);
        if (!scope.Allowed) return GetNextQuestionResult.FailForbidden(scope.Reason!);
        var nextQuestion = await _recommendationEngine.GetNextQuestionAsync(
            centerId,
            studentId,
            subjectId,
            utcNow,
            cancellationToken);

        if (nextQuestion is null)
        {
            return GetNextQuestionResult.FailNotFound("No next question or recommendation available for this subject.");
        }

        if (scope.TopicIds is not null && (nextQuestion.Topic is null || !scope.Includes(nextQuestion.Topic.NodeId)))
            return GetNextQuestionResult.FailNotFound("Lộ trình hiện tại không khớp giáo trình của lớp đang xem. Hãy tạo/cập nhật lộ trình trong Đang học.");

        return GetNextQuestionResult.Success(nextQuestion);
    }
}
