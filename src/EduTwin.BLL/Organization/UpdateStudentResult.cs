using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class UpdateStudentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public StudentDto? Data { get; }

    private UpdateStudentResult(bool isSuccess, string? errorCode, StudentDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpdateStudentResult Success(StudentDto data) => new(true, null, data);
    public static UpdateStudentResult Failure(string errorCode) => new(false, errorCode, null);
}
