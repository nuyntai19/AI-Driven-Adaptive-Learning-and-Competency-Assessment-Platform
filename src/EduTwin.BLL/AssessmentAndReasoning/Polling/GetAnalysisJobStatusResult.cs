using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.AssessmentAndReasoning.Polling;

public sealed class GetAnalysisJobStatusResult
{
    private GetAnalysisJobStatusResult(
        bool isSuccess,
        string? errorCode,
        AnalysisJobStatusDataDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public AnalysisJobStatusDataDto? Data { get; }

    public static GetAnalysisJobStatusResult Success(
        AnalysisJobStatusDataDto data) =>
        new(true, null, data);

    public static GetAnalysisJobStatusResult ValidationFailed() =>
        new(false, ErrorCodes.ValidationFailed, null);

    public static GetAnalysisJobStatusResult Forbidden() =>
        new(false, ErrorCodes.ForbiddenResource, null);

    public static GetAnalysisJobStatusResult NotFound() =>
        new(false, ErrorCodes.ResourceNotFound, null);

    public static GetAnalysisJobStatusResult Failure(string errorCode) =>
        new(false, errorCode, null);
}
