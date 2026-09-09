using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public sealed record AuthorizationRoleResult(
    bool IsSuccess,
    string? ErrorCode,
    AuthorizationRoleDto? Data)
{
    public static AuthorizationRoleResult Success(AuthorizationRoleDto data) =>
        new(true, null, data);

    public static AuthorizationRoleResult Failure(string errorCode) =>
        new(false, errorCode, null);
}

public sealed record ListAuthorizationRolesResult(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyList<AuthorizationRoleDto> Data,
    long TotalItems,
    int TotalPages)
{
    public static ListAuthorizationRolesResult Success(
        IReadOnlyList<AuthorizationRoleDto> data,
        long totalItems,
        int totalPages) => new(true, null, data, totalItems, totalPages);

    public static ListAuthorizationRolesResult Failure(string errorCode) =>
        new(false, errorCode, [], 0, 0);
}
