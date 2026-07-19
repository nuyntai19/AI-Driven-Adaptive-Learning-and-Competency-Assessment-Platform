using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class UpdateTeacherResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public TeacherDto? Data { get; }

    private UpdateTeacherResult(bool isSuccess, string? errorCode, TeacherDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpdateTeacherResult Success(TeacherDto data) => new(true, null, data);
    public static UpdateTeacherResult Failure(string errorCode) => new(false, errorCode, null);
}
