using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class GetStudentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public StudentDetailDto? Data { get; }

    private GetStudentResult(bool isSuccess, string? errorCode, StudentDetailDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static GetStudentResult Success(StudentDetailDto data) => new(true, null, data);
    public static GetStudentResult Failure(string errorCode) => new(false, errorCode, null);
}
