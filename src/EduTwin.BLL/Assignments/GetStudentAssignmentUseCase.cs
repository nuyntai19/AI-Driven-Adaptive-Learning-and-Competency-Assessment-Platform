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

        var totalQuestionCount = assignmentQuestions.Count;
        var scorePerQuestion = totalQuestionCount > 0 ? 10.0m / totalQuestionCount : 0m;

        var questionsDto = assignmentQuestions.Select(aq =>
        {
            var latestAttempt = latestAttemptsByQuestion.TryGetValue(aq.QuestionId, out var att) ? att : null;
            var analysis = latestAttempt != null && analysesByAttemptId.TryGetValue(latestAttempt.AttemptId, out var foundAnalysis)
                ? foundAnalysis
                : null;
            var isVoided = aq.IsVoided;
            var voidReason = aq.IsVoided ? aq.VoidReason : null;
            var voidedScore = aq.IsVoided ? scorePerQuestion : (decimal?)null;

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
                AttemptStatus = latestAttempt?.Status.ToString(),
                LatestAttempt = latestAttempt != null ? new StudentQuestionAttemptDto
                {
                    AttemptId = latestAttempt.AttemptId.ToString(),
                    Status = isVoided ? nameof(AttemptStatus.Completed) : latestAttempt.Status.ToString(),
                    FinalAnswer = latestAttempt.FinalAnswer,
                    AnswerDisplayLatex = latestAttempt.AnswerDisplayLatex,
                    ReasoningText = latestAttempt.ReasoningText,
                    Confidence = latestAttempt.Confidence,
                    TimeSpentSeconds = latestAttempt.TimeSpentSeconds,
                    AnswerChanges = latestAttempt.AnswerChanges,
                    Skipped = latestAttempt.Skipped,
                    SubmittedAt = latestAttempt.CreatedAt,
                    IsCorrect = isVoided ? true : (analysis?.OverrideIsCorrect ?? latestAttempt.IsCorrect),
                    AwardedScore = isVoided ? (aq.Question?.MaxScore ?? 10m) : (analysis?.OverrideAwardedScore ?? latestAttempt.AwardedScore),
                    MaxScore = aq.Question?.MaxScore
                } : null,
                SubmittedAnswer = latestAttempt?.FinalAnswer,
                SubmittedAnswerDisplayLatex = latestAttempt?.AnswerDisplayLatex,
                SubmittedReasoning = latestAttempt?.ReasoningText,
                SubmittedAttemptId = latestAttempt?.AttemptId,
                HasAttachment = latestAttempt != null && hasAttachmentSet.Contains(latestAttempt.AttemptId),
                EffectiveIsCorrect = isVoided ? true : (analysis?.OverrideIsCorrect ?? latestAttempt?.IsCorrect),
                IsVoided = isVoided,
                VoidReason = voidReason,
                VoidedScore = voidedScore
            };
        }).ToList();

        DateTime? effectiveExpiresAt = null;
        int? remainingSeconds = null;

        var allQuestionsSubmitted = assignmentQuestions.Count > 0 && assignmentQuestions.All(q =>
            q.IsVoided || latestAttemptsByQuestion.ContainsKey(q.QuestionId));
        var isSubmitted = progress.Status == ProgressStatus.Completed || allQuestionsSubmitted;
        DateTime? submittedAt = isSubmitted ? progress.CompletedAt : null;
        if (isSubmitted && !submittedAt.HasValue)
        {
            // Legacy or still-processing submissions may not have CompletedAt.
            // Use only persisted attempts belonging to this assignment's questions.
            submittedAt = latestAttemptsByQuestion.Values.Where(a => questionIds.Contains(a.QuestionId))
                .Select(a => (DateTime?)a.CreatedAt).Max();
        }
        int? elapsedSeconds = isSubmitted && submittedAt.HasValue && progress.StartedAt.HasValue
            ? (int)Math.Clamp((submittedAt.Value - progress.StartedAt.Value).TotalSeconds, 0, int.MaxValue)
            : null;
        var clockAt = isSubmitted ? submittedAt : utcNow;

        if (progress.Assignment.TimeLimitMinutes.HasValue && progress.Assignment.TimeLimitMinutes.Value > 0)
        {
            if (progress.StartedAt.HasValue)
            {
                var timeLimitExpiresAt = progress.StartedAt.Value.AddMinutes(progress.Assignment.TimeLimitMinutes.Value);
                effectiveExpiresAt = progress.Assignment.DueAt.HasValue && progress.Assignment.DueAt.Value < timeLimitExpiresAt
                    ? progress.Assignment.DueAt.Value
                    : timeLimitExpiresAt;

                if (clockAt.HasValue)
                {
                    var diff = (long)(effectiveExpiresAt.Value - clockAt.Value).TotalSeconds;
                    remainingSeconds = diff > 0 ? (int)Math.Min(diff, int.MaxValue) : 0;
                }
            }
            else
            {
                // Not started yet: full time limit, countdown begins only when started
                effectiveExpiresAt = progress.Assignment.DueAt;
                remainingSeconds = isSubmitted ? null : progress.Assignment.TimeLimitMinutes.Value * 60;
            }
        }
        else
        {
            // For untimed assignments: if teacher set a DueAt, countdown to DueAt; otherwise no timer.
            if (progress.Assignment.DueAt.HasValue)
            {
                effectiveExpiresAt = progress.Assignment.DueAt.Value;
                if (clockAt.HasValue)
                {
                    var diff = (long)(progress.Assignment.DueAt.Value - clockAt.Value).TotalSeconds;
                    remainingSeconds = diff > 0 ? (int)Math.Min(diff, int.MaxValue) : 0;
                }
            }
            else
            {
                effectiveExpiresAt = null;
                remainingSeconds = null;
            }
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
            IsSubmitted = isSubmitted,
            SubmittedAt = submittedAt,
            ElapsedSeconds = elapsedSeconds,
            EffectiveExpiresAt = effectiveExpiresAt,
            RemainingSeconds = remainingSeconds,
            Progress = new StudentAssignmentProgressDto
            {
                Status = isSubmitted ? nameof(ProgressStatus.Completed)
                    : AssignmentStatusHelper.GetEffectiveProgressStatus(progress.Status, effectiveExpiresAt, utcNow).ToString(),
                CompletedQuestionCount = (int)progress.CompletedQuestionCount,
                TotalQuestionCount = (int)progress.TotalQuestionCount
            },
            Questions = questionsDto,
            CanRetake = false,
            Summary = await _resultCalculator.CalculateForSingleAssignmentAsync(centerId.Value, currentUserId.Value, assignmentId, cancellationToken),
            DraftAnswers = progress.Status != ProgressStatus.Completed && !string.IsNullOrWhiteSpace(progress.DraftAnswersJson)
                ? DraftAnswersHelper.ParseDraft(progress.DraftAnswersJson).Answers
                : null,
            DraftVersion = progress.Status != ProgressStatus.Completed && !string.IsNullOrWhiteSpace(progress.DraftAnswersJson)
                ? DraftAnswersHelper.ParseDraft(progress.DraftAnswersJson).Version
                : null
        };

        var response = new StudentAssignmentDetailResponse
        {
            Data = detailDto
        };

        return GetStudentAssignmentResult.Success(response);
    }
}
