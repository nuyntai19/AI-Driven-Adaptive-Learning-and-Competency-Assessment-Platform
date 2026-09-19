using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.DAL;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.Assignments;

public class GetStudentAssignmentUseCase : IGetStudentAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public GetStudentAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<GetStudentAssignmentResult> ExecuteAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var currentUserId = _tenantContext.UserId;
        var centerId = _tenantContext.CenterId;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var progress = await _dbContext.StudentAssignmentProgresses
            .AsNoTracking()
            .Include(p => p.Assignment)
            .FirstOrDefaultAsync(p =>
                p.CenterId == centerId &&
                p.StudentId == currentUserId &&
                p.AssignmentId == assignmentId &&
                !p.IsDeleted, cancellationToken);

        if (progress == null || progress.Assignment == null || progress.Assignment.IsDeleted)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (progress.Assignment.Status != AssignmentStatus.Published && progress.Assignment.Status != AssignmentStatus.Closed)
        {
            return GetStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var assignmentQuestions = await _dbContext.AssignmentQuestions
            .AsNoTracking()
            .Include(aq => aq.Question)
            .Where(aq => aq.CenterId == centerId && aq.AssignmentId == assignmentId)
            .OrderBy(aq => aq.OrderIndex)
            .ToListAsync(cancellationToken);

        var questionIds = assignmentQuestions.Select(aq => aq.QuestionId).ToList();

        var questionOptions = await _dbContext.QuestionOptions
            .AsNoTracking()
            .Where(o => o.CenterId == centerId && questionIds.Contains(o.QuestionId))
            .ToListAsync(cancellationToken);

        // Fetch user attempts to map attemptStatus and latestAttempt for each question
        var attemptsList = await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.CenterId == centerId && a.StudentId == currentUserId && a.AssignmentId == assignmentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestAttemptsByQuestion = attemptsList
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.First());

        var questionsDto = assignmentQuestions.Select(aq =>
        {
            var latestAttempt = latestAttemptsByQuestion.TryGetValue(aq.QuestionId, out var att) ? att : null;
            return new StudentQuestionDto
            {
                QuestionId = aq.QuestionId.ToString(),
                QuestionType = aq.Question!.QuestionType.ToString(),
                Difficulty = aq.Question.Difficulty,
                QuestionText = aq.Question.QuestionText,
                EstimatedTimeSeconds = (int)aq.Question.EstimatedTimeSeconds,
                ReasoningRequired = aq.Question.ReasoningRequired,
                LanguageCode = aq.Question.LanguageCode,
                Options = questionOptions
                    .Where(o => o.QuestionId == aq.QuestionId)
                    .OrderBy(o => o.OrderIndex)
                    .Select(o => new StudentQuestionOptionDto
                    {
                        OptionId = o.OptionId.ToString(),
                        Label = o.OptionLabel,
                        Text = o.OptionText
                    }).ToList(),
                AttemptStatus = latestAttempt?.Status.ToString(),
                LatestAttempt = latestAttempt != null ? new StudentQuestionAttemptDto
                {
                    AttemptId = latestAttempt.AttemptId.ToString(),
                    Status = latestAttempt.Status.ToString(),
                    FinalAnswer = latestAttempt.FinalAnswer,
                    ReasoningText = latestAttempt.ReasoningText,
                    Confidence = latestAttempt.Confidence,
                    TimeSpentSeconds = latestAttempt.TimeSpentSeconds,
                    AnswerChanges = latestAttempt.AnswerChanges,
                    Skipped = latestAttempt.Skipped,
                    SubmittedAt = latestAttempt.CreatedAt
                } : null
            };
        }).ToList();

        var detailDto = new StudentAssignmentDetailDto
        {
            AssignmentId = progress.AssignmentId.ToString(),
            Title = progress.Assignment.Title,
            Instructions = progress.Assignment.Instructions,
            DueAt = progress.Assignment.DueAt,
            Progress = new StudentAssignmentProgressDto
            {
                Status = AssignmentStatusHelper.GetEffectiveProgressStatus(progress.Status, progress.Assignment.DueAt, utcNow).ToString(),
                CompletedQuestionCount = (int)progress.CompletedQuestionCount,
                TotalQuestionCount = (int)progress.TotalQuestionCount
            },
            Questions = questionsDto,
            CanRetake = false
        };

        var response = new StudentAssignmentDetailResponse
        {
            Data = detailDto
        };

        return GetStudentAssignmentResult.Success(response);
    }
}
