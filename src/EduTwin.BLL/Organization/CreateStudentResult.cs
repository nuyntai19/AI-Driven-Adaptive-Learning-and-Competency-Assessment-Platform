using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class CreateStudentResult
{
    public bool IsSuccess { get; private set; }
    public StudentDetailDto? Data { get; private set; }
    public string? ErrorCode { get; private set; }

    private CreateStudentResult(bool isSuccess, StudentDetailDto? data, string? errorCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
    }

    public static CreateStudentResult Success(StudentDetailDto data) => new(true, data, null);
    public static CreateStudentResult Failure(string errorCode) => new(false, null, errorCode);
}
