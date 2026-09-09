namespace EduTwin.Contracts.Common;

public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthUserDisabled = "AUTH_USER_DISABLED";
    public const string AuthRefreshInvalid = "AUTH_REFRESH_INVALID";
    public const string AuthPermissionRequired = "AUTH_PERMISSION_REQUIRED";
    public const string AuthPrivilegeEscalation = "AUTH_PRIVILEGE_ESCALATION";
    public const string AuthorizationVersionStale = "AUTHORIZATION_VERSION_STALE";
    public const string RoleAccountTypeMismatch = "ROLE_ACCOUNT_TYPE_MISMATCH";
    public const string LastTenantAdmin = "LAST_TENANT_ADMIN";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string ForbiddenResource = "FORBIDDEN_RESOURCE";
    public const string DuplicateResource = "DUPLICATE_RESOURCE";
    public const string DuplicateSubmission = "DUPLICATE_SUBMISSION";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string DagCycleDetected = "DAG_CYCLE_DETECTED";
    public const string AssignmentNotAvailable = "ASSIGNMENT_NOT_AVAILABLE";
    public const string QuestionReasoningRequired = "QUESTION_REASONING_REQUIRED";
}
