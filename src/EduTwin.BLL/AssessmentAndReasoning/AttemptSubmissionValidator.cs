using System.Globalization;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning;

public sealed class AttemptSubmissionValidator : IAttemptSubmissionValidator
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly PreliminaryGraderFactory _graderFactory;

    public AttemptSubmissionValidator(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        PreliminaryGraderFactory graderFactory)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _graderFactory = graderFactory;
    }

    public async Task<AttemptSubmissionValidationResult> ValidateAsync(
        SubmitAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveStudentContext(out var centerId, out var studentId))
        {
            return AttemptSubmissionValidationResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!TryValidateRequest(request, out var questionId))
        {
            return AttemptSubmissionValidationResult.Failure(ErrorCodes.ValidationFailed);
        }

        var isActiveStudent = await _dbContext.Students
            .AsNoTracking()
            .AnyAsync(
                student =>
                    student.CenterId == centerId &&
                    student.StudentId == studentId &&
                    !student.IsDeleted,
                cancellationToken);

        var isActiveStudentUser = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.CenterId == centerId &&
                    user.UserId == studentId &&
                    !user.IsDeleted &&
                    user.RoleName == UserRole.Student &&
                    user.Status == UserStatus.Active,
                cancellationToken);

        if (!isActiveStudent || !isActiveStudentUser)
        {
            return AttemptSubmissionValidationResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var existingAttempt = await _dbContext.Attempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attempt =>
                    attempt.CenterId == centerId &&
                    attempt.StudentId == studentId &&
                    attempt.ClientSubmissionId == request.ClientSubmissionId,
                cancellationToken);

        if (existingAttempt is not null)
        {
            if (!HasSamePayload(existingAttempt, request, questionId))
            {
                return AttemptSubmissionValidationResult.Failure(ErrorCodes.DuplicateSubmission);
            }

            return AttemptSubmissionValidationResult.Success(
                FromExistingAttempt(existingAttempt, request.DrawingUploadToken));
        }

        var question = await _dbContext.Questions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.CenterId == centerId &&
                    candidate.QuestionId == questionId &&
                    !candidate.IsDeleted &&
                    candidate.Status == QuestionStatus.Active,
                cancellationToken);

        if (question is null || !IsSupportedLanguage(question.LanguageCode))
        {
            return AttemptSubmissionValidationResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (request.AssignmentId.HasValue)
        {
            var assignment = await _dbContext.Assignments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate =>
                        candidate.CenterId == centerId &&
                        candidate.AssignmentId == request.AssignmentId.Value &&
                        !candidate.IsDeleted,
                    cancellationToken);

            if (assignment is null)
            {
                return AttemptSubmissionValidationResult.Failure(ErrorCodes.ResourceNotFound);
            }

            if (assignment.Status != AssignmentStatus.Published)
            {
                return AttemptSubmissionValidationResult.Failure(ErrorCodes.AssignmentNotAvailable);
            }

            var isTarget = await _dbContext.AssignmentTargets
                .AsNoTracking()
                .AnyAsync(
                    target =>
                        target.CenterId == centerId &&
                        target.AssignmentId == request.AssignmentId.Value &&
                        target.StudentId == studentId,
                    cancellationToken);

            var belongsToAssignment = await _dbContext.AssignmentQuestions
                .AsNoTracking()
                .AnyAsync(
                    assignmentQuestion =>
                        assignmentQuestion.CenterId == centerId &&
                        assignmentQuestion.AssignmentId == request.AssignmentId.Value &&
                        assignmentQuestion.QuestionId == questionId,
                    cancellationToken);

            if (!isTarget || !belongsToAssignment)
            {
                return AttemptSubmissionValidationResult.Failure(ErrorCodes.AssignmentNotAvailable);
            }
        }

        if (question.ReasoningRequired && string.IsNullOrWhiteSpace(request.ReasoningText))
        {
            return AttemptSubmissionValidationResult.Failure(ErrorCodes.QuestionReasoningRequired);
        }

        if (request.AnswerDisplayLatex != null && request.AnswerDisplayLatex.Length > 2048)
        {
            return AttemptSubmissionValidationResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (request.DrawingUploadToken != null && request.DrawingUploadToken.Length > 8192)
        {
            return AttemptSubmissionValidationResult.Failure(ErrorCodes.ValidationFailed);
        }

        var options = question.QuestionType == QuestionType.MultipleChoice
            ? await _dbContext.QuestionOptions
                .AsNoTracking()
                .Where(option =>
                    option.CenterId == centerId &&
                    option.QuestionId == questionId &&
                    !option.IsDeleted)
                .OrderBy(option => option.OrderIndex)
                .ThenBy(option => option.OptionId)
                .ToListAsync(cancellationToken)
            : [];

        var preliminaryGrade = _graderFactory
            .GetGrader(question.QuestionType)
            .Grade(
                request.FinalAnswer,
                question.CorrectAnswer,
                new PreliminaryGrading.QuestionGradingContext
                {
                    EvaluationMode = question.AnswerEvaluationMode,
                    MaxScore = question.MaxScore,
                    Criteria = question.GradingCriteria,
                    Options = options
                });

        return AttemptSubmissionValidationResult.Success(new ValidatedAttemptSubmission
        {
            CenterId = centerId,
            StudentId = studentId,
            ClientSubmissionId = request.ClientSubmissionId,
            QuestionId = questionId,
            AssignmentId = request.AssignmentId,
            FinalAnswer = request.FinalAnswer,
            ReasoningText = request.ReasoningText,
            AnswerDisplayLatex = request.AnswerDisplayLatex,
            DrawingUploadToken = request.DrawingUploadToken,
            TimeSpentSeconds = request.TimeSpentSeconds,
            Confidence = request.Confidence,
            AnswerChanges = request.AnswerChanges,
            Skipped = request.Skipped,
            ReasoningLanguage = question.LanguageCode,
            IsCorrect = preliminaryGrade.IsCorrect,
            AwardedScore = preliminaryGrade.Score
        });
    }

    private bool TryResolveStudentContext(out Guid centerId, out Guid studentId)
    {
        centerId = _tenantContext.CenterId ?? Guid.Empty;
        studentId = _tenantContext.UserId ?? Guid.Empty;

        return _tenantContext.IsResolved &&
               centerId != Guid.Empty &&
               studentId != Guid.Empty &&
               string.Equals(
                   _tenantContext.Role,
                   nameof(UserRole.Student),
                   StringComparison.Ordinal);
    }

    private static bool TryValidateRequest(
        SubmitAttemptRequest? request,
        out ulong questionId)
    {
        questionId = 0;

        if (request is null ||
            request.ClientSubmissionId == Guid.Empty ||
            request.AssignmentId == Guid.Empty ||
            request.FinalAnswer is null ||
            (!request.Skipped && string.IsNullOrWhiteSpace(request.FinalAnswer)) ||
            request.Confidence < 0m ||
            request.Confidence > 100m ||
            string.IsNullOrEmpty(request.QuestionId))
        {
            return false;
        }

        foreach (var character in request.QuestionId)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return ulong.TryParse(
                   request.QuestionId,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out questionId) &&
               questionId > 0;
    }

    private static bool HasSamePayload(
        Attempt existingAttempt,
        SubmitAttemptRequest request,
        ulong questionId) =>
        existingAttempt.QuestionId == questionId &&
        existingAttempt.AssignmentId == request.AssignmentId &&
        string.Equals(existingAttempt.FinalAnswer, request.FinalAnswer, StringComparison.Ordinal) &&
        string.Equals(existingAttempt.ReasoningText, request.ReasoningText, StringComparison.Ordinal) &&
        string.Equals(existingAttempt.AnswerDisplayLatex, request.AnswerDisplayLatex, StringComparison.Ordinal) &&
        existingAttempt.TimeSpentSeconds == request.TimeSpentSeconds &&
        existingAttempt.Confidence == request.Confidence &&
        existingAttempt.AnswerChanges == request.AnswerChanges &&
        existingAttempt.Skipped == request.Skipped;

    private static ValidatedAttemptSubmission FromExistingAttempt(
        Attempt attempt,
        string? drawingUploadToken) => new()
    {
        CenterId = attempt.CenterId,
        StudentId = attempt.StudentId,
        ClientSubmissionId = attempt.ClientSubmissionId,
        QuestionId = attempt.QuestionId,
        AssignmentId = attempt.AssignmentId,
        FinalAnswer = attempt.FinalAnswer,
        ReasoningText = attempt.ReasoningText,
        AnswerDisplayLatex = attempt.AnswerDisplayLatex,
        DrawingUploadToken = drawingUploadToken,
        TimeSpentSeconds = attempt.TimeSpentSeconds,
        Confidence = attempt.Confidence,
        AnswerChanges = attempt.AnswerChanges,
        Skipped = attempt.Skipped,
        ReasoningLanguage = attempt.ReasoningLanguage,
        IsCorrect = attempt.IsCorrect,
        AwardedScore = attempt.AwardedScore,
        ExistingAttemptId = attempt.AttemptId
    };

    private static bool IsSupportedLanguage(string languageCode) =>
        string.Equals(languageCode, "vi", StringComparison.Ordinal) ||
        string.Equals(languageCode, "en", StringComparison.Ordinal);
}
