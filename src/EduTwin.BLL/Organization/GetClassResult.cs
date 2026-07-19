using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class GetClassResult
{
    public bool IsSuccess { get; }
    public ClassDto? Data { get; }
    public string? ErrorCode { get; }

    private GetClassResult(bool isSuccess, ClassDto? data, string? errorCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
    }

    public static GetClassResult Success(ClassDto data) => new(true, data, null);
    public static GetClassResult Failure(string errorCode) => new(false, null, errorCode);
}
