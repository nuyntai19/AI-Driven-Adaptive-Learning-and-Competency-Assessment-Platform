using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class ListStudentsResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public System.Collections.Generic.IReadOnlyList<StudentDto>? Data { get; }
    public int TotalItems { get; }
    public int TotalPages { get; }

    private ListStudentsResult(bool isSuccess, string? errorCode, System.Collections.Generic.IReadOnlyList<StudentDto>? data, int totalItems, int totalPages)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        TotalItems = totalItems;
        TotalPages = totalPages;
    }

    public static ListStudentsResult Success(System.Collections.Generic.IReadOnlyList<StudentDto> data, int totalItems, int totalPages) => new(true, null, data, totalItems, totalPages);
    public static ListStudentsResult ValidationFailed() => new(false, EduTwin.Contracts.Common.ErrorCodes.ValidationFailed, null, 0, 0);
    public static ListStudentsResult Failure(string errorCode) => new(false, errorCode, null, 0, 0);
}
