namespace EduTwin.BLL.Organization;

public class RemoveStudentFromClassResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }

    private RemoveStudentFromClassResult(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static RemoveStudentFromClassResult Success() => new(true, null);
    public static RemoveStudentFromClassResult Failure(string errorCode) => new(false, errorCode);
}
