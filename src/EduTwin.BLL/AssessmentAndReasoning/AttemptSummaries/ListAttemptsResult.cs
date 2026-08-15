using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;

public sealed class ListAttemptsResult
{
    private ListAttemptsResult(
        bool isSuccess,
        IReadOnlyList<AttemptSummaryDto>? data,
        int page,
        int pageSize,
        long totalItems,
        int totalPages,
        string? errorCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = totalPages;
        ErrorCode = errorCode;
    }

    public bool IsSuccess { get; }
    public IReadOnlyList<AttemptSummaryDto>? Data { get; }
    public int Page { get; }
    public int PageSize { get; }
    public long TotalItems { get; }
    public int TotalPages { get; }
    public string? ErrorCode { get; }

    public static ListAttemptsResult Success(
        IReadOnlyList<AttemptSummaryDto> data,
        int page,
        int pageSize,
        long totalItems,
        int totalPages) =>
        new(true, data, page, pageSize, totalItems, totalPages, null);

    public static ListAttemptsResult ValidationFailed() => Failure(ErrorCodes.ValidationFailed);
    public static ListAttemptsResult Forbidden() => Failure(ErrorCodes.ForbiddenResource);
    public static ListAttemptsResult NotFound() => Failure(ErrorCodes.ResourceNotFound);

    public static ListAttemptsResult Failure(string errorCode) =>
        new(false, null, 0, 0, 0, 0, errorCode);
}
