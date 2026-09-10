using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public sealed class ListTeacherReviewQueueResult
{
    private ListTeacherReviewQueueResult(
        bool isSuccess,
        IReadOnlyList<TeacherReviewQueueItemDto>? data,
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
    public IReadOnlyList<TeacherReviewQueueItemDto>? Data { get; }
    public int Page { get; }
    public int PageSize { get; }
    public long TotalItems { get; }
    public int TotalPages { get; }
    public string? ErrorCode { get; }

    public static ListTeacherReviewQueueResult Success(
        IReadOnlyList<TeacherReviewQueueItemDto> data,
        int page,
        int pageSize,
        long totalItems,
        int totalPages) =>
        new(true, data, page, pageSize, totalItems, totalPages, null);

    public static ListTeacherReviewQueueResult ValidationFailed() => Failure(ErrorCodes.ValidationFailed);
    public static ListTeacherReviewQueueResult Forbidden() => Failure(ErrorCodes.ForbiddenResource);
    public static ListTeacherReviewQueueResult NotFound() => Failure(ErrorCodes.ResourceNotFound);

    private static ListTeacherReviewQueueResult Failure(string errorCode) =>
        new(false, null, 0, 0, 0, 0, errorCode);
}
