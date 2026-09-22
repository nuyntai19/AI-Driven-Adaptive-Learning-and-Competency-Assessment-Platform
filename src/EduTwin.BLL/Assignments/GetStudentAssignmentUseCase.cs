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

        var attempts = await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.CenterId == centerId && a.StudentId == currentUserId && a.AssignmentId == assignmentId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.QuestionId,
                a.AttemptId,
                a.Status,
                a.FinalAnswer,
                a.ReasoningText
            })
            .ToListAsync(cancellationToken);

        var latestAttempts = attempts
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.First());

        var attemptIds = latestAttempts.Values.Select(x => x.AttemptId).ToList();
        var attachments = await _dbContext.AttemptAttachments
            .AsNoTracking()
            .Where(aa => aa.CenterId == centerId && attemptIds.Contains(aa.AttemptId))
            .Select(aa => aa.AttemptId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var hasAttachmentSet = attachments.ToHashSet();

        var questionsDto = assignmentQuestions.Select(aq =>
        {
            var latest = latestAttempts.TryGetValue(aq.QuestionId, out var att) ? att : null;
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
                AttemptStatus = latest?.Status.ToString(),
                SubmittedAnswer = latest?.FinalAnswer,
                SubmittedReasoning = latest?.ReasoningText,
                SubmittedAttemptId = latest?.AttemptId,
                HasAttachment = latest != null && hasAttachmentSet.Contains(latest.AttemptId)
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
            Questions = questionsDto
        };

        var response = new StudentAssignmentDetailResponse
        {
            Data = detailDto
        };

        return GetStudentAssignmentResult.Success(response);
    }
}
