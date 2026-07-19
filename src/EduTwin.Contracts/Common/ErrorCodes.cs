namespace EduTwin.Contracts.Common;

public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthUserDisabled = "AUTH_USER_DISABLED";
    public const string AuthRefreshInvalid = "AUTH_REFRESH_INVALID";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string ForbiddenResource = "FORBIDDEN_RESOURCE";
    public const string DuplicateResource = "DUPLICATE_RESOURCE";
}
