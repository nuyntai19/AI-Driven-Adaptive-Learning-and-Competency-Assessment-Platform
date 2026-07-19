using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class CreateTeacherResult
{
    public bool IsSuccess { get; }
    public TeacherDto? Data { get; }
    public string? ErrorCode { get; }

    private CreateTeacherResult(bool isSuccess, TeacherDto? data, string? errorCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
    }

    public static CreateTeacherResult Success(TeacherDto data) => new(true, data, null);
    public static CreateTeacherResult Failure(string errorCode) => new(false, null, errorCode);
}
