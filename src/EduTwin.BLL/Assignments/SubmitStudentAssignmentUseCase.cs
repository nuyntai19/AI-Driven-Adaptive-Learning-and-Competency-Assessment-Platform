using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Assignments;

public class SubmitStudentAssignmentUseCase : ISubmitStudentAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly PreliminaryGraderFactory _graderFactory;
    private readonly IAttemptAttachmentTokenService? _attachmentTokens;
    private readonly IAttemptAttachmentStorage? _attachmentStorage;

    public SubmitStudentAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        PreliminaryGraderFactory? graderFactory = null,
        IAttemptAttachmentTokenService? attachmentTokens = null,
        IAttemptAttachmentStorage? attachmentStorage = null)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _graderFactory = graderFactory ?? new PreliminaryGraderFactory(
            new MultipleChoiceGrader(),
            new ShortAnswerGrader(),
            new EssayGrader());
        _attachmentTokens = attachmentTokens;
        _attachmentStorage = attachmentStorage;
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
        if (await StudentAssignmentScope.SuspendedAsync(_dbContext, centerId, assignment.ClassId, cancellationToken))
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);

        DateTime? effectiveExpiresAt = null;
        if (assignment.DueAt.HasValue)
        {
            effectiveExpiresAt = assignment.DueAt.Value;
        }
        if (assignment.TimeLimitMinutes.HasValue && assignment.TimeLimitMinutes.Value > 0 && progress.StartedAt.HasValue)
        {
            var timeLimitExpiresAt = progress.StartedAt.Value.AddMinutes(assignment.TimeLimitMinutes.Value);
            effectiveExpiresAt = effectiveExpiresAt.HasValue && effectiveExpiresAt.Value < timeLimitExpiresAt
                ? effectiveExpiresAt.Value
                : timeLimitExpiresAt;
        }

        List<AssignmentDraftAnswerItemDto> answersToProcess;
        var hasClientAnswers = request.Answers != null && request.Answers.Count > 0;
        var isAutoFinalizeExpired = effectiveExpiresAt.HasValue && utcNow > effectiveExpiresAt.Value;

        if (progress.StartedAt == null)
        {
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        if (isAutoFinalizeExpired)
        {
            // Past deadline: ignore any new client answers submitted after deadline.
            // Only allow finalization using draft answers pre-saved on server BEFORE deadline.
            // If student started but has no pre-saved draft (e.g. empty exam), finalize with empty answers (all questions recorded as SKIPPED).
            if (!string.IsNullOrWhiteSpace(progress.DraftAnswersJson) &&
                progress.DraftSavedAt.HasValue &&
                effectiveExpiresAt.HasValue &&
                progress.DraftSavedAt.Value <= effectiveExpiresAt.Value)
            {
                answersToProcess = DraftAnswersHelper.ParseDraft(progress.DraftAnswersJson).Answers;
            }
            else
            {
                answersToProcess = new List<AssignmentDraftAnswerItemDto>();
            }
        }
        else
        {
            // On time submission
            if (hasClientAnswers)
            {
                answersToProcess = request.Answers!;
            }
            else
            {
                // On time without payload: use server draft if any, otherwise empty
                answersToProcess = DraftAnswersHelper.ParseDraft(progress.DraftAnswersJson).Answers;
            }
        }

        var assignmentQuestions = await _dbContext.AssignmentQuestions
            .Include(aq => aq.Question)
            .Where(aq => aq.CenterId == centerId && aq.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        // Sanitize and deduplicate answersToProcess: only keep questions belonging to this assignment
        var validAssignmentQuestionMap = assignmentQuestions.ToDictionary(aq => (long)aq.QuestionId);
        var validQuestionIds = validAssignmentQuestionMap.Keys.ToHashSet();

        var sanitizedAnswers = answersToProcess
            .Where(a => validQuestionIds.Contains(a.QuestionId))
            .GroupBy(a => a.QuestionId)
            .Select(g => g.Last())
            .ToList();

        var answersByQuestionId = sanitizedAnswers.ToDictionary(a => a.QuestionId);

        var existingAttempts = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId && a.StudentId == currentUserId)
            .ToListAsync(cancellationToken);

        var existingQuestionIds = existingAttempts.Select(a => a.QuestionId).ToHashSet();

        // Validate attachment tokens and guard against already used tokens before starting transaction
        var attachmentTokensInBatch = sanitizedAnswers
            .Where(a => !string.IsNullOrWhiteSpace(a.DrawingUploadToken))
            .Select(a => a.DrawingUploadToken!)
            .ToList();

        if (attachmentTokensInBatch.Count > 0)
        {
            var nonces = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in attachmentTokensInBatch)
            {
                var payload = ResolveAttachmentPayload(token, centerId, currentUserId, true);
                if (payload == null)
                {
                    return SubmitStudentAssignmentResult.Failure(ErrorCodes.ValidationFailed);
                }
                if (!nonces.Add(payload.UploadNonce))
                {
                    return SubmitStudentAssignmentResult.Failure(ErrorCodes.UploadTokenAlreadyUsed);
                }
            }

            var alreadyUsedInDb = await AttemptAttachmentNonceQuery.ForUploadNonces(
                    _dbContext.AttemptAttachments.AsNoTracking(), centerId, nonces)
                .AnyAsync(cancellationToken);

            if (alreadyUsedInDb)
            {
                return SubmitStudentAssignmentResult.Failure(ErrorCodes.UploadTokenAlreadyUsed);
            }
        }

        // Pre-validate all answers before entering transaction
        foreach (var aq in assignmentQuestions)
        {
            if (aq.IsVoided || existingQuestionIds.Contains(aq.QuestionId))
            {
                continue;
            }

            answersByQuestionId.TryGetValue((long)aq.QuestionId, out var ans);
            var rawAnswer = ans?.FinalAnswer?.Trim();
            var isSkipped = string.IsNullOrWhiteSpace(rawAnswer) || rawAnswer == "SKIPPED";
            var reasoningText = isSkipped ? null : ans?.ReasoningText?.Trim();

            // Reasoning requirement check: enforced during active submission, but bypassed when auto-finalizing expired draft
            if (!isAutoFinalizeExpired && !isSkipped && aq.Question?.ReasoningRequired == true && string.IsNullOrWhiteSpace(reasoningText))
            {
                return SubmitStudentAssignmentResult.Failure(ErrorCodes.QuestionReasoningRequired);
            }
        }

        // Load QuestionOptions for all MultipleChoice questions in this assignment in a single query
        var questionIds = assignmentQuestions.Select(aq => aq.QuestionId).ToList();
        var questionOptions = await _dbContext.QuestionOptions
            .AsNoTracking()
            .Where(o => o.CenterId == centerId && questionIds.Contains(o.QuestionId) && !o.IsDeleted)
            .OrderBy(o => o.OrderIndex)
            .ThenBy(o => o.OptionId)
            .ToListAsync(cancellationToken);
        var optionsByQuestionId = questionOptions
            .GroupBy(o => o.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        int createdAttemptCount = 0;
        AIAnalysisJob? lastCreatedJob = null;
        var attachmentsToPromote = new List<(Attempt Attempt, AttachmentUploadTokenPayload Payload)>();
        var promotedAttachments = new List<PromotedAttemptAttachment>();

        try
        {
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

                var questionMaxScore = aq.Question?.MaxScore > 0 ? aq.Question.MaxScore : (aq.Points > 0 ? aq.Points : 10m);

                // Preliminary grading using the standard PreliminaryGraderFactory
                PreliminaryGradingResult preliminaryGrade;
                if (aq.IsVoided)
                {
                    preliminaryGrade = new PreliminaryGradingResult
                    {
                        IsCorrect = true,
                        Score = questionMaxScore,
                        Feedback = "Câu hỏi đã được đánh dấu có sai sót đề và được tự động công nhận trọn điểm.",
                        ReasonCode = PreliminaryGradingReasonCodes.VoidedQuestion
                    };
                }
                else if (isSkipped)
                {
                    preliminaryGrade = new PreliminaryGradingResult
                    {
                        IsCorrect = false,
                        Score = 0m,
                        Feedback = "Skipped",
                        ReasonCode = PreliminaryGradingReasonCodes.NoAnswer
                    };
                }
                else if (aq.Question != null)
                {
                    optionsByQuestionId.TryGetValue(aq.QuestionId, out var opts);
                    preliminaryGrade = _graderFactory
                        .GetGrader(aq.Question.QuestionType)
                        .Grade(
                            finalAnswer,
                            aq.Question.CorrectAnswer,
                            new QuestionGradingContext
                            {
                                EvaluationMode = aq.Question.AnswerEvaluationMode,
                                MaxScore = questionMaxScore,
                                Criteria = aq.Question.GradingCriteria,
                                Options = opts ?? new List<QuestionOption>()
                            });
                }
                else
                {
                    preliminaryGrade = new PreliminaryGradingResult
                    {
                        IsCorrect = false,
                        Score = 0m,
                        Feedback = "No question found",
                        ReasonCode = PreliminaryGradingReasonCodes.NoAnswer
                    };
                }

                attempt.IsCorrect = preliminaryGrade.IsCorrect;
                attempt.AwardedScore = preliminaryGrade.Score;
                attempt.PreliminaryGradingReasonCode = preliminaryGrade.ReasonCode;

                _dbContext.Attempts.Add(attempt);
                createdAttemptCount++;

                if (!string.IsNullOrWhiteSpace(ans?.DrawingUploadToken))
                {
                    var payload = ResolveAttachmentPayload(ans.DrawingUploadToken, centerId, currentUserId, true);
                    if (payload != null)
                    {
                        attachmentsToPromote.Add((attempt, payload));
                    }
                }

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
                lastCreatedJob = job;
            }

            var nonVoidedValidQuestionIds = assignmentQuestions
                .Where(aq => !aq.IsVoided)
                .Select(aq => (ulong)aq.QuestionId)
                .ToHashSet();

            var actuallyAnsweredCount = existingQuestionIds
                .Concat(sanitizedAnswers.Where(a => !string.IsNullOrWhiteSpace(a.FinalAnswer) && a.FinalAnswer != "SKIPPED").Select(a => (ulong)a.QuestionId))
                .Where(qId => nonVoidedValidQuestionIds.Contains(qId))
                .Distinct()
                .Count();

            var maxAllowedCompleted = progress.TotalQuestionCount > 0
                ? progress.TotalQuestionCount
                : (uint)nonVoidedValidQuestionIds.Count;
            progress.CompletedQuestionCount = Math.Min((uint)actuallyAnsweredCount, maxAllowedCompleted);
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

            // Promote and persist scratchpad attachments
            if (attachmentsToPromote.Count > 0 && _attachmentStorage != null)
            {
                foreach (var (att, payload) in attachmentsToPromote)
                {
                    var promotedAttachment = await _attachmentStorage.PromoteToPermanentAsync(payload, cancellationToken);
                    promotedAttachments.Add(promotedAttachment);
                    _dbContext.AttemptAttachments.Add(new AttemptAttachment
                    {
                        CenterId = centerId,
                        AttemptId = att.AttemptId,
                        UploadNonce = payload.UploadNonce,
                        FileName = payload.FileName,
                        ContentType = "image/png",
                        StorageKey = promotedAttachment.StorageKey,
                        FileSizeBytes = payload.FileSizeBytes,
                        CreatedAt = utcNow,
                        CreatedBy = currentUserId
                    });
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception) when (IsAttachmentDuplicate(exception))
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            _dbContext.ChangeTracker.Clear();
            foreach (var promoted in promotedAttachments)
            {
                await DeleteNewlyPromotedAttachmentAsync(promoted);
            }
            return SubmitStudentAssignmentResult.Failure(ErrorCodes.UploadTokenAlreadyUsed);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            _dbContext.ChangeTracker.Clear();
            foreach (var promoted in promotedAttachments)
            {
                await DeleteNewlyPromotedAttachmentAsync(promoted);
            }

            var reloadedProgress = await _dbContext.StudentAssignmentProgresses
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.CenterId == centerId && p.StudentId == currentUserId && p.AssignmentId == assignmentId, CancellationToken.None);

            if (reloadedProgress?.Status == ProgressStatus.Completed)
            {
                return SubmitStudentAssignmentResult.Success(new SubmitAssignmentResponseDto
                {
                    AssignmentId = assignmentId.ToString("D"),
                    SubmittedAttemptsCount = 0,
                    IsCompleted = true,
                    Message = "Bài tập đã được nộp và ghi nhận trước đó."
                });
            }

            return SubmitStudentAssignmentResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            _dbContext.ChangeTracker.Clear();
            foreach (var promoted in promotedAttachments)
            {
                await DeleteNewlyPromotedAttachmentAsync(promoted);
            }
            throw;
        }

        // Job ID retrieval after SaveChanges guarantees auto-increment primary key is populated
        string? lastJobId = lastCreatedJob != null && lastCreatedJob.AnalysisJobId > 0
            ? lastCreatedJob.AnalysisJobId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : null;

        return SubmitStudentAssignmentResult.Success(new SubmitAssignmentResponseDto
        {
            AssignmentId = assignmentId.ToString("D"),
            SubmittedAttemptsCount = createdAttemptCount,
            LastAnalysisJobId = lastJobId,
            IsCompleted = true,
            Message = "Nộp bài tập thành công."
        });
    }

    private async Task DeleteNewlyPromotedAttachmentAsync(PromotedAttemptAttachment? promotedAttachment)
    {
        if (promotedAttachment is not { WasNewlyPromoted: true } || _attachmentStorage is null)
        {
            return;
        }

        try
        {
            var isReferenced = await _dbContext.AttemptAttachments
                .AsNoTracking()
                .AnyAsync(candidate => candidate.StorageKey == promotedAttachment.StorageKey);

            if (!isReferenced)
            {
                await _attachmentStorage.DeletePermanentAsync(promotedAttachment.StorageKey, CancellationToken.None);
            }
        }
        catch
        {
            // The periodic orphan sweep remains the crash/retry safety net.
        }
    }

    private static bool IsAttachmentDuplicate(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("ux_attempt_attachments_center_id_upload_nonce", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("ux_attempt_attachments_center_id_storage_key", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("ux_attempt_attachments_center_id_attempt_id", StringComparison.OrdinalIgnoreCase) ||
                (current.Message.Contains("attempt_attachments", StringComparison.OrdinalIgnoreCase) &&
                 current.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    private AttachmentUploadTokenPayload? ResolveAttachmentPayload(
        string? drawingUploadToken,
        Guid centerId,
        Guid studentId,
        bool enforceExpiry)
    {
        if (string.IsNullOrWhiteSpace(drawingUploadToken)) return null;
        if (_attachmentTokens is null || _attachmentStorage is null ||
            !_attachmentTokens.TryRead(drawingUploadToken, out var payload) || payload is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (payload.CenterId != centerId ||
            payload.StudentId != studentId ||
            string.IsNullOrWhiteSpace(payload.UploadNonce) ||
            string.IsNullOrWhiteSpace(payload.Sha256Hex) ||
            payload.Sha256Hex.Length != 64 ||
            !payload.Sha256Hex.All(Uri.IsHexDigit) ||
            payload.FileSizeBytes is < 1 or > 5_242_880 ||
            (enforceExpiry && payload.ExpiresAtUtc <= now))
        {
            return null;
        }

        return payload;
    }
}
