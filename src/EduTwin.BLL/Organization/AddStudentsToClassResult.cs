using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class AddStudentsToClassResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public AddStudentsToClassDto? Data { get; }

    private AddStudentsToClassResult(bool isSuccess, string? errorCode, string? errorMessage, AddStudentsToClassDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Data = data;
    }

    public static AddStudentsToClassResult Success(AddStudentsToClassDto data) => new(true, null, null, data);
    public static AddStudentsToClassResult Failure(string errorCode, string? errorMessage = null) => new(false, errorCode, errorMessage, null);
}
