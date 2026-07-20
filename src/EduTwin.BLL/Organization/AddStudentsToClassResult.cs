using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class AddStudentsToClassResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public AddStudentsToClassDto? Data { get; }

    private AddStudentsToClassResult(bool isSuccess, string? errorCode, AddStudentsToClassDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static AddStudentsToClassResult Success(AddStudentsToClassDto data) => new(true, null, data);
    public static AddStudentsToClassResult Failure(string errorCode) => new(false, errorCode, null);
}
