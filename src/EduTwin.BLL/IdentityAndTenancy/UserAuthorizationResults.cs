using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed record UserAuthorizationResult(
    bool IsSuccess,
    string? ErrorCode,
    UserAuthorizationDto? Data)
{
    public static UserAuthorizationResult Success(UserAuthorizationDto data) =>
        new(true, null, data);

    public static UserAuthorizationResult Failure(string errorCode) =>
        new(false, errorCode, null);
}

public sealed record ListAuthorizationAuditResult(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyList<AuthorizationAuditDto> Data,
    long TotalItems,
    int TotalPages)
{
    public static ListAuthorizationAuditResult Success(
        IReadOnlyList<AuthorizationAuditDto> data,
        long totalItems,
        int totalPages) => new(true, null, data, totalItems, totalPages);

    public static ListAuthorizationAuditResult Failure(string errorCode) =>
        new(false, errorCode, [], 0, 0);
}

public sealed record ListAuthorizationUsersResult(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyList<AuthorizationUserDto> Data,
    long TotalItems,
    int TotalPages)
{
    public static ListAuthorizationUsersResult Success(
        IReadOnlyList<AuthorizationUserDto> data,
        long totalItems,
        int totalPages) => new(true, null, data, totalItems, totalPages);

    public static ListAuthorizationUsersResult Failure(string errorCode) =>
        new(false, errorCode, [], 0, 0);
}
