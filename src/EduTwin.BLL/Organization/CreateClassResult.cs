using System.Collections.Generic;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class CreateClassResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public ClassDto? Data { get; }

    private CreateClassResult(bool isSuccess, string? errorCode, ClassDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static CreateClassResult Success(ClassDto data) => new(true, null, data);
    public static CreateClassResult Failure(string errorCode) => new(false, errorCode, null);
}
