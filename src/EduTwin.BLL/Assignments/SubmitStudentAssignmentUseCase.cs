using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Assignments;

public class SubmitStudentAssignmentUseCase : ISubmitStudentAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public SubmitStudentAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<SubmitStudentAssignmentResult> ExecuteAsync(
        Guid assignmentId,
        SubmitAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var currentUserId = _tenantContext.UserId.Value;
        var centerId = _tenantContext.CenterId.Value;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CenterId == centerId && a.AssignmentId == assignmentId && !a.IsDeleted, cancellationToken);

        if (assignment == null)
        {
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignment.Status != AssignmentStatus.Published)
        {
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        var isTarget = await _dbContext.AssignmentTargets
            .AsNoTracking()
            .AnyAsync(at => at.CenterId == centerId && at.AssignmentId == assignmentId && at.StudentId == currentUserId, cancellationToken);

        if (!isTarget)
        {
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.ForbiddenResource);
        }

        var progress = await _dbContext.StudentAssignmentProgresses
            .FirstOrDefaultAsync(p =>
                p.CenterId == centerId &&
                p.StudentId == currentUserId &&
                p.AssignmentId == assignmentId &&
                !p.IsDeleted, cancellationToken);

        if (progress == null)
        {
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (progress.Status == ProgressStatus.Completed)
        {
            return SubmitStudentAssignmentResult.Success(new SubmitAssignmentResponseDto
            {
                AssignmentId = assignmentId.ToString("D"),
                SubmittedAttemptsCount = 0,
                IsCompleted = true,
                Message = "Bài tập đã được nộp và ghi nhận trước đó."
            });
        }

        // Calculate authoritative expiration
        DateTime? effectiveExpiresAt = null;
        if (assignment.TimeLimitMinutes.HasValue && assignment.TimeLimitMinutes.Value > 0)
        {
            if (progress.StartedAt.HasValue)
            {
                var timeLimitExpiresAt = progress.StartedAt.Value.AddMinutes(assignment.TimeLimitMinutes.Value);
                effectiveExpiresAt = assignment.DueAt.HasValue && assignment.DueAt.Value < timeLimitExpiresAt
                    ? assignment.DueAt.Value
                    : timeLimitExpiresAt;
            }
        }
        else if (assignment.DueAt.HasValue)
        {
            effectiveExpiresAt = assignment.DueAt.Value;
        }

        List<AssignmentDraftAnswerItemDto> answersToProcess;
        var hasClientAnswers = request.Answers != null && request.Answers.Count > 0;

        if (hasClientAnswers)
        {
            if (effectiveExpiresAt.HasValue && utcNow > effectiveExpiresAt.Value)
            {
                // New answers submitted past deadline:
                // Only allow finalization using draft answers pre-saved on server BEFORE deadline
                if (!string.IsNullOrWhiteSpace(progress.DraftAnswersJson) &&
                    progress.DraftSavedAt.HasValue &&
                    progress.DraftSavedAt.Value <= effectiveExpiresAt.Value)
                {
                    answersToProcess = JsonSerializer.Deserialize<List<AssignmentDraftAnswerItemDto>>(progress.DraftAnswersJson)
                        ?? new List<AssignmentDraftAnswerItemDto>();
                }
                else
                {
                    return SubmitStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);
                }
            }
            else
            {
                // Single unified submission received on time
                answersToProcess = request.Answers!;
            }
        }
        else
        {
            // Auto-submit or finalize without payload: use server draft saved before deadline
            if (!string.IsNullOrWhiteSpace(progress.DraftAnswersJson) &&
                (!effectiveExpiresAt.HasValue || (progress.DraftSavedAt.HasValue && progress.DraftSavedAt.Value <= effectiveExpiresAt.Value)))
            {
                answersToProcess = JsonSerializer.Deserialize<List<AssignmentDraftAnswerItemDto>>(progress.DraftAnswersJson)
                    ?? new List<AssignmentDraftAnswerItemDto>();
            }
            else
            {
                answersToProcess = new List<AssignmentDraftAnswerItemDto>();
            }
        }

        var assignmentQuestions = await _dbContext.AssignmentQuestions
            .Include(aq => aq.Question)
            .Where(aq => aq.CenterId == centerId && aq.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        var existingAttempts = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId && a.StudentId == currentUserId)
            .ToListAsync(cancellationToken);

        var existingQuestionIds = existingAttempts.Select(a => a.QuestionId).ToHashSet();
        var answersByQuestionId = answersToProcess.ToDictionary(a => a.QuestionId);

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        int createdAttemptCount = 0;
        string? lastJobId = null;

        foreach (var aq in assignmentQuestions)
        {
            if (aq.IsVoided || existingQuestionIds.Contains(aq.QuestionId))
            {
                continue;
            }

            answersByQuestionId.TryGetValue((long)aq.QuestionId, out var ans);
            var rawAnswer = ans?.FinalAnswer?.Trim();
            var isSkipped = string.IsNullOrWhiteSpace(rawAnswer) || rawAnswer == "SKIPPED";
            var finalAnswer = isSkipped ? "SKIPPED" : rawAnswer!;
            var reasoningText = isSkipped ? null : ans?.ReasoningText?.Trim();
            var timeSpent = ans?.TimeSpentSeconds ?? 0;
            var confidence = ans?.Confidence ?? 80;
            var answerChanges = ans?.AnswerChanges ?? 0;
            var displayLatex = ans?.AnswerDisplayLatex;

            var attempt = new Attempt
            {
                CenterId = centerId,
                StudentId = currentUserId,
                AssignmentId = assignmentId,
                QuestionId = aq.QuestionId,
                FinalAnswer = finalAnswer,
                ReasoningText = reasoningText,
                AnswerDisplayLatex = displayLatex,
                TimeSpentSeconds = (uint)Math.Max(0, timeSpent),
                Confidence = Math.Clamp(confidence, 0, 100),
                AnswerChanges = (uint)Math.Max(0, answerChanges),
                Skipped = isSkipped,
                ReasoningLanguage = aq.Question?.LanguageCode ?? "vi",
                Status = AttemptStatus.PendingAnalysis,
                ClientSubmissionId = Guid.NewGuid(),
                CreatedAt = utcNow,
                CreatedBy = currentUserId,
                UpdatedAt = utcNow,
                RowVersion = 1ul
            };

            if (isSkipped)
            {
                attempt.IsCorrect = false;
                attempt.AwardedScore = 0m;
                attempt.PreliminaryGradingReasonCode = "STUDENT_SKIPPED";
            }
            else if (aq.Question != null && aq.Question.QuestionType == QuestionType.MultipleChoice)
            {
                var correctOption = await _dbContext.QuestionOptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.QuestionId == aq.QuestionId && o.IsCorrect, cancellationToken);

                if (correctOption != null)
                {
                    var isCorrect = string.Equals(correctOption.OptionText?.Trim(), finalAnswer, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(correctOption.OptionLabel?.Trim(), finalAnswer, StringComparison.OrdinalIgnoreCase);
                    var maxScore = aq.Points > 0 ? aq.Points : (aq.Question.MaxScore > 0 ? aq.Question.MaxScore : 10m);
                    attempt.IsCorrect = isCorrect;
                    attempt.AwardedScore = isCorrect ? maxScore : 0m;
                }
            }

            _dbContext.Attempts.Add(attempt);
            createdAttemptCount++;

            var job = new AIAnalysisJob
            {
                CenterId = centerId,
                Attempt = attempt,
                Status = AIJobStatus.Pending,
                RetryCount = 0,
                AvailableAt = utcNow,
                CorrelationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
                CreatedAt = utcNow,
                CreatedBy = currentUserId,
                UpdatedAt = utcNow
            };
            _dbContext.AIAnalysisJobs.Add(job);
            lastJobId = job.AnalysisJobId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var actuallyAnsweredCount = existingQuestionIds
            .Concat(answersToProcess.Where(a => !string.IsNullOrWhiteSpace(a.FinalAnswer) && a.FinalAnswer != "SKIPPED").Select(a => (ulong)a.QuestionId))
            .Distinct()
            .Count();

        progress.CompletedQuestionCount = (uint)actuallyAnsweredCount;
        progress.Status = ProgressStatus.Completed;
        progress.CompletedAt ??= utcNow;
        progress.DraftAnswersJson = null;
        progress.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Pending;
        progress.FinalReviewedByUserId = null;
        progress.FinalReviewedAt = null;
        progress.FinalTeacherNote = null;
        progress.FinalReviewVersion++;
        progress.IsOverallAiCommentStale = true;
        progress.UpdatedAt = utcNow;
        progress.UpdatedBy = currentUserId;
        progress.RowVersion++;

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return SubmitStudentAssignmentResult.Success(new SubmitAssignmentResponseDto
        {
            AssignmentId = assignmentId.ToString("D"),
            SubmittedAttemptsCount = createdAttemptCount,
            LastAnalysisJobId = lastJobId,
            IsCompleted = true,
            Message = "Nộp bài tập thành công."
        });
    }
}
