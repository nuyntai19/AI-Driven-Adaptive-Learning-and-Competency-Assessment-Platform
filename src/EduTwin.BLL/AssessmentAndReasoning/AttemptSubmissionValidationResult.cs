namespace EduTwin.BLL.AssessmentAndReasoning;

public sealed class AttemptSubmissionValidationResult
{
    private AttemptSubmissionValidationResult(
        bool isSuccess,
        string? errorCode,
        ValidatedAttemptSubmission? submission)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Submission = submission;
    }

    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public ValidatedAttemptSubmission? Submission { get; }

    public static AttemptSubmissionValidationResult Success(ValidatedAttemptSubmission submission) =>
        new(true, null, submission);

    public static AttemptSubmissionValidationResult Failure(string errorCode) =>
        new(false, errorCode, null);
}
