using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class UpdateClassResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public ClassDto? Data { get; }

    private UpdateClassResult(bool isSuccess, string? errorCode, ClassDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpdateClassResult Success(ClassDto data) => new(true, null, data);
    public static UpdateClassResult Failure(string errorCode) => new(false, errorCode, null);
}
