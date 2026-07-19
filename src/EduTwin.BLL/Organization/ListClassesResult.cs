using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class ListClassesResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public System.Collections.Generic.IReadOnlyList<ClassDto>? Data { get; }
    public int TotalItems { get; }
    public int TotalPages { get; }

    private ListClassesResult(bool isSuccess, string? errorCode, System.Collections.Generic.IReadOnlyList<ClassDto>? data, int totalItems, int totalPages)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        TotalItems = totalItems;
        TotalPages = totalPages;
    }

    public static ListClassesResult Success(System.Collections.Generic.IReadOnlyList<ClassDto> data, int totalItems, int totalPages) => new(true, null, data, totalItems, totalPages);
    public static ListClassesResult Failure(string errorCode) => new(false, errorCode, null, 0, 0);
}
