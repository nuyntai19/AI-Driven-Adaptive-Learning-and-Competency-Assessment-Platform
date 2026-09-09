using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed class ListPermissionsResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public IReadOnlyList<PermissionDto> Data { get; private init; } = [];
    public long TotalItems { get; private init; }
    public int TotalPages { get; private init; }

    public static ListPermissionsResult Success(
        IReadOnlyList<PermissionDto> data,
        long totalItems,
        int totalPages) => new()
    {
        IsSuccess = true,
        Data = data,
        TotalItems = totalItems,
        TotalPages = totalPages
    };

    public static ListPermissionsResult Failure(string errorCode) => new()
    {
        ErrorCode = errorCode
    };
}
