namespace EduTwin.BLL.Organization;

public class ResetAccountPasswordResult
{
    public bool IsSuccess { get; }
    public string? TargetUserId { get; }
    public string? NewRowVersion { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private ResetAccountPasswordResult(
        bool isSuccess,
        string? targetUserId,
        string? newRowVersion,
        string? errorCode,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        TargetUserId = targetUserId;
        NewRowVersion = newRowVersion;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static ResetAccountPasswordResult Success(string targetUserId, string newRowVersion) =>
        new(true, targetUserId, newRowVersion, null, null);

    public static ResetAccountPasswordResult Failure(string errorCode, string? errorMessage = null) =>
        new(false, null, null, errorCode, errorMessage);
}
