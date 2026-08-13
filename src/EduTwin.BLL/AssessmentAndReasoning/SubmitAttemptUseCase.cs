using System.Globalization;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning;

public sealed class SubmitAttemptUseCase : ISubmitAttemptUseCase
{
    private const string AttemptIdempotencyConstraint =
        "ux_attempts_center_id_student_id_client_submission_id";

    private readonly EduTwinDbContext _dbContext;
    private readonly IAttemptSubmissionValidator _validator;
    private readonly TimeProvider _timeProvider;

    public SubmitAttemptUseCase(
        EduTwinDbContext dbContext,
        IAttemptSubmissionValidator validator,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<SubmitAttemptResult> ExecuteAsync(
        SubmitAttemptRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);

        if (!validation.IsSuccess)
        {
            if (string.IsNullOrWhiteSpace(validation.ErrorCode))
            {
                throw new InvalidOperationException("Attempt validation failed without an error code.");
            }

            return SubmitAttemptResult.Failure(validation.ErrorCode);
        }

        var submission = validation.Submission ??
            throw new InvalidOperationException("Successful attempt validation did not return a submission.");

        cancellationToken.ThrowIfCancellationRequested();

        if (submission.ExistingAttemptId.HasValue)
        {
            return await LoadReplayAsync(submission, cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var boundaryAttempt = await _dbContext.Attempts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    attempt =>
                        attempt.CenterId == submission.CenterId &&
                        attempt.StudentId == submission.StudentId &&
                        attempt.ClientSubmissionId == submission.ClientSubmissionId,
                    cancellationToken);

            if (boundaryAttempt is not null)
            {
                if (!HasSamePayload(boundaryAttempt, submission))
                {
                    return SubmitAttemptResult.Failure(ErrorCodes.DuplicateSubmission);
                }

                return await LoadReplayAsync(
                    submission,
                    boundaryAttempt.AttemptId,
                    cancellationToken);
            }

            StudentAssignmentProgress? progress = null;
            if (submission.AssignmentId.HasValue)
            {
                progress = await _dbContext.StudentAssignmentProgresses
                    .SingleOrDefaultAsync(
                        candidate =>
                            candidate.CenterId == submission.CenterId &&
                            candidate.AssignmentId == submission.AssignmentId.Value &&
                            candidate.StudentId == submission.StudentId,
                        cancellationToken);

                if (progress is null)
                {
                    return SubmitAttemptResult.Failure(ErrorCodes.AssignmentNotAvailable);
                }
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var attempt = CreateAttempt(submission, now);
            var job = CreateAnalysisJob(
                submission,
                attempt,
                SanitizeCorrelationId(correlationId),
                now);

            if (progress?.Status == ProgressStatus.NotStarted)
            {
                progress.Status = ProgressStatus.InProgress;
                progress.StartedAt ??= now;
                progress.UpdatedAt = now;
                progress.UpdatedBy = submission.StudentId;
            }

            _dbContext.Attempts.Add(attempt);
            _dbContext.AIAnalysisJobs.Add(job);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return SubmitAttemptResult.Success(ToAcceptedData(attempt, job));
        }
        catch (DbUpdateException exception) when (IsAttemptIdempotencyDuplicate(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();

            var replay = await ResolveConcurrentDuplicateAsync(submission, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<SubmitAttemptResult?> ResolveConcurrentDuplicateAsync(
        ValidatedAttemptSubmission submission,
        CancellationToken cancellationToken)
    {
        var existingAttempt = await _dbContext.Attempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attempt =>
                    attempt.CenterId == submission.CenterId &&
                    attempt.StudentId == submission.StudentId &&
                    attempt.ClientSubmissionId == submission.ClientSubmissionId,
                cancellationToken);

        if (existingAttempt is null)
        {
            return null;
        }

        if (!HasSamePayload(existingAttempt, submission))
        {
            return SubmitAttemptResult.Failure(ErrorCodes.DuplicateSubmission);
        }

        return await LoadReplayAsync(
            submission,
            existingAttempt.AttemptId,
            cancellationToken);
    }

    private Task<SubmitAttemptResult> LoadReplayAsync(
        ValidatedAttemptSubmission submission,
        CancellationToken cancellationToken) =>
        LoadReplayAsync(
            submission,
            submission.ExistingAttemptId ??
                throw new InvalidOperationException("Idempotent replay is missing the persisted attempt ID."),
            cancellationToken);

    private async Task<SubmitAttemptResult> LoadReplayAsync(
        ValidatedAttemptSubmission submission,
        ulong attemptId,
        CancellationToken cancellationToken)
    {
        var attempt = await _dbContext.Attempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.CenterId == submission.CenterId &&
                    candidate.StudentId == submission.StudentId &&
                    candidate.ClientSubmissionId == submission.ClientSubmissionId &&
                    candidate.AttemptId == attemptId,
                cancellationToken) ??
            throw new InvalidOperationException("The validated replay attempt no longer exists in its tenant scope.");

        var job = await _dbContext.AIAnalysisJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.CenterId == submission.CenterId &&
                    candidate.AttemptId == attempt.AttemptId,
                cancellationToken) ??
            throw new InvalidOperationException("The persisted attempt does not have its required analysis job.");

        return SubmitAttemptResult.Success(ToAcceptedData(attempt, job));
    }

    private static Attempt CreateAttempt(
        ValidatedAttemptSubmission submission,
        DateTime now) =>
        new()
        {
            CenterId = submission.CenterId,
            StudentId = submission.StudentId,
            QuestionId = submission.QuestionId,
            AssignmentId = submission.AssignmentId,
            FinalAnswer = submission.FinalAnswer,
            ReasoningText = submission.ReasoningText,
            IsCorrect = submission.IsCorrect,
            AwardedScore = submission.AwardedScore,
            TimeSpentSeconds = submission.TimeSpentSeconds,
            Confidence = submission.Confidence,
            AnswerChanges = submission.AnswerChanges,
            Skipped = submission.Skipped,
            ReasoningLanguage = submission.ReasoningLanguage,
            Status = AttemptStatus.PendingAnalysis,
            ClientSubmissionId = submission.ClientSubmissionId,
            CreatedAt = now,
            CreatedBy = submission.StudentId,
            UpdatedAt = now
        };

    private static AIAnalysisJob CreateAnalysisJob(
        ValidatedAttemptSubmission submission,
        Attempt attempt,
        string correlationId,
        DateTime now) =>
        new()
        {
            CenterId = submission.CenterId,
            Attempt = attempt,
            Status = AIJobStatus.Pending,
            RetryCount = 0,
            AvailableAt = now,
            CorrelationId = correlationId,
            CreatedAt = now,
            CreatedBy = submission.StudentId,
            UpdatedAt = now
        };

    private static SubmitAttemptAcceptedDataDto ToAcceptedData(
        Attempt attempt,
        AIAnalysisJob job)
    {
        var attemptId = attempt.AttemptId.ToString(CultureInfo.InvariantCulture);
        var jobId = job.AnalysisJobId.ToString(CultureInfo.InvariantCulture);

        return new SubmitAttemptAcceptedDataDto
        {
            AttemptId = attemptId,
            AnalysisJobId = jobId,
            AttemptStatus = attempt.Status.ToString(),
            JobStatus = job.Status.ToString(),
            PollUrl = $"/api/v1/learning/analysis-jobs/{jobId}",
            PollAfterMilliseconds = 3000
        };
    }

    private static bool HasSamePayload(
        Attempt attempt,
        ValidatedAttemptSubmission submission) =>
        attempt.QuestionId == submission.QuestionId &&
        attempt.AssignmentId == submission.AssignmentId &&
        string.Equals(attempt.FinalAnswer, submission.FinalAnswer, StringComparison.Ordinal) &&
        string.Equals(attempt.ReasoningText, submission.ReasoningText, StringComparison.Ordinal) &&
        attempt.TimeSpentSeconds == submission.TimeSpentSeconds &&
        attempt.Confidence == submission.Confidence &&
        attempt.AnswerChanges == submission.AnswerChanges &&
        attempt.Skipped == submission.Skipped;

    private static string SanitizeCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return "unknown";
        }

        var sanitized = string.Concat(
                correlationId.Where(character => !char.IsControl(character)))
            .Trim();

        if (sanitized.Length == 0)
        {
            return "unknown";
        }

        return sanitized.Length <= 64 ? sanitized : sanitized[..64];
    }

    private static bool IsAttemptIdempotencyDuplicate(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    AttemptIdempotencyConstraint,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
