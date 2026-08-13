using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning;

public sealed class SubmitAttemptResult
{
    private SubmitAttemptResult(
        bool isSuccess,
        string? errorCode,
        SubmitAttemptAcceptedDataDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public SubmitAttemptAcceptedDataDto? Data { get; }

    public static SubmitAttemptResult Success(SubmitAttemptAcceptedDataDto data) =>
        new(true, null, data);

    public static SubmitAttemptResult Failure(string errorCode) =>
        new(false, errorCode, null);
}
