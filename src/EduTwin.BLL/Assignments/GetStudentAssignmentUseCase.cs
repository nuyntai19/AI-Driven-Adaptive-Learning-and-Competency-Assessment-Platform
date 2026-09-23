using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using QuestionStatus = EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Assignments;

public class GetStudentAssignmentUseCase : IGetStudentAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly IAssignmentResultCalculator _resultCalculator;

    public GetStudentAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        IAssignmentResultCalculator resultCalculator)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _resultCalculator = resultCalculator;
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

        // Fetch user attempts to map attemptStatus, latestAttempt, submitted answers, and attachments for each question
        var attemptsList = await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.CenterId == centerId && a.StudentId == currentUserId && a.AssignmentId == assignmentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestAttemptsByQuestion = attemptsList
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.First());

        var attemptIds = latestAttemptsByQuestion.Values.Select(x => x.AttemptId).ToList();
        var attachments = await _dbContext.AttemptAttachments
            .AsNoTracking()
            .Where(aa => aa.CenterId == centerId && attemptIds.Contains(aa.AttemptId))
            .Select(aa => aa.AttemptId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var hasAttachmentSet = attachments.ToHashSet();
        var analysesByAttemptId = attemptIds.Count == 0
            ? new System.Collections.Generic.Dictionary<ulong, EduTwin.DAL.AssessmentAndReasoning.ReasoningAnalysis>()
            : await _dbContext.ReasoningAnalyses
                .AsNoTracking()
                .Where(a => a.CenterId == centerId && attemptIds.Contains(a.AttemptId))
                .ToDictionaryAsync(a => a.AttemptId, cancellationToken);

        var questionsDto = assignmentQuestions.Select(aq =>
        {
            var latestAttempt = latestAttemptsByQuestion.TryGetValue(aq.QuestionId, out var att) ? att : null;
            var analysis = latestAttempt != null && analysesByAttemptId.TryGetValue(latestAttempt.AttemptId, out var foundAnalysis)
                ? foundAnalysis
                : null;
            var isVoided = aq.Question?.Status == QuestionStatus.Archived;
            var fullScore = aq.Points > 0 ? aq.Points : (aq.Question?.MaxScore ?? 1.00m);
            return new StudentQuestionDto
            {
                QuestionId = aq.QuestionId.ToString(),
                QuestionType = aq.Question!.QuestionType.ToString(),
                Difficulty = aq.Question.Difficulty,
                QuestionText = aq.Question.QuestionText,
                EstimatedTimeSeconds = (int)aq.Question.EstimatedTimeSeconds,
                ReasoningRequired = aq.Question.ReasoningRequired,
                LanguageCode = aq.Question.LanguageCode,
                AnswerEvaluationMode = aq.Question.AnswerEvaluationMode.ToString(),
                Options = questionOptions
                    .Where(o => o.QuestionId == aq.QuestionId)
                    .OrderBy(o => o.OrderIndex)
                    .Select(o => new StudentQuestionOptionDto
                    {
                        OptionId = o.OptionId.ToString(),
                        Label = o.OptionLabel,
                        Text = o.OptionText
                    }).ToList(),
                AttemptStatus = isVoided ? nameof(AttemptStatus.Completed) : latestAttempt?.Status.ToString(),
                LatestAttempt = latestAttempt != null ? new StudentQuestionAttemptDto
                {
                    AttemptId = latestAttempt.AttemptId.ToString(),
                    Status = isVoided ? nameof(AttemptStatus.Completed) : latestAttempt.Status.ToString(),
                    FinalAnswer = latestAttempt.FinalAnswer,
                    ReasoningText = latestAttempt.ReasoningText,
                    Confidence = latestAttempt.Confidence,
                    TimeSpentSeconds = latestAttempt.TimeSpentSeconds,
                    AnswerChanges = latestAttempt.AnswerChanges,
                    Skipped = latestAttempt.Skipped,
                    SubmittedAt = latestAttempt.CreatedAt,
                    IsCorrect = isVoided ? true : latestAttempt.IsCorrect,
                    AwardedScore = isVoided ? fullScore : latestAttempt.AwardedScore,
                    MaxScore = aq.Question?.MaxScore
                } : null,
                SubmittedAnswer = latestAttempt?.FinalAnswer,
                SubmittedReasoning = latestAttempt?.ReasoningText,
                SubmittedAttemptId = latestAttempt?.AttemptId,
                HasAttachment = latestAttempt != null && hasAttachmentSet.Contains(latestAttempt.AttemptId),
                EffectiveIsCorrect = isVoided ? true : (analysis?.OverrideIsCorrect ?? latestAttempt?.IsCorrect)
            };
        }).ToList();

        DateTime? effectiveExpiresAt = null;
        if (progress.Assignment.DueAt.HasValue && progress.Assignment.TimeLimitMinutes.HasValue && progress.StartedAt.HasValue)
        {
            var timeLimitExpiresAt = progress.StartedAt.Value.AddMinutes(progress.Assignment.TimeLimitMinutes.Value);
            effectiveExpiresAt = progress.Assignment.DueAt.Value < timeLimitExpiresAt ? progress.Assignment.DueAt.Value : timeLimitExpiresAt;
        }
        else if (progress.Assignment.TimeLimitMinutes.HasValue && progress.StartedAt.HasValue)
        {
            effectiveExpiresAt = progress.StartedAt.Value.AddMinutes(progress.Assignment.TimeLimitMinutes.Value);
        }
        else if (progress.Assignment.DueAt.HasValue)
        {
            effectiveExpiresAt = progress.Assignment.DueAt.Value;
        }

        int? remainingSeconds = null;
        if (effectiveExpiresAt.HasValue)
        {
            var diff = (long)(effectiveExpiresAt.Value - utcNow).TotalSeconds;
            remainingSeconds = diff > 0 ? (int)Math.Min(diff, int.MaxValue) : 0;
        }

        EduTwin.DAL.Organization.Class? assignmentClass = null;
        if (progress.Assignment.ClassId != Guid.Empty)
        {
            assignmentClass = await _dbContext.Classes
                .AsNoTracking()
                .Include(c => c.Subject)
                .FirstOrDefaultAsync(c => c.CenterId == centerId && c.ClassId == progress.Assignment.ClassId, cancellationToken);
        }

        var detailDto = new StudentAssignmentDetailDto
        {
            AssignmentId = progress.AssignmentId.ToString(),
            Title = progress.Assignment.Title,
            Instructions = progress.Assignment.Instructions,
            DueAt = progress.Assignment.DueAt,
            SubjectId = assignmentClass?.SubjectId.ToString(),
            SubjectName = assignmentClass?.Subject?.SubjectName,
            TimeLimitMinutes = progress.Assignment.TimeLimitMinutes,
            StartedAt = progress.StartedAt,
            EffectiveExpiresAt = effectiveExpiresAt,
            RemainingSeconds = remainingSeconds,
            Progress = new StudentAssignmentProgressDto
            {
                Status = AssignmentStatusHelper.GetEffectiveProgressStatus(progress.Status, effectiveExpiresAt, utcNow).ToString(),
                CompletedQuestionCount = (int)progress.CompletedQuestionCount,
                TotalQuestionCount = (int)progress.TotalQuestionCount
            },
            Questions = questionsDto,
            CanRetake = false,
            Summary = await _resultCalculator.CalculateForSingleAssignmentAsync(centerId.Value, currentUserId.Value, assignmentId, cancellationToken)
        };

        var response = new StudentAssignmentDetailResponse
        {
            Data = detailDto
        };

        return GetStudentAssignmentResult.Success(response);
    }
}
