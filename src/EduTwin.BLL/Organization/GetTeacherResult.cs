using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class GetTeacherResult
{
    public bool IsSuccess { get; }
    public TeacherDto? Data { get; }
    public string? ErrorCode { get; }

    private GetTeacherResult(bool isSuccess, TeacherDto? data, string? errorCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
    }

    public static GetTeacherResult Success(TeacherDto data) => new(true, data, null);
    public static GetTeacherResult Failure(string errorCode) => new(false, null, errorCode);
}
