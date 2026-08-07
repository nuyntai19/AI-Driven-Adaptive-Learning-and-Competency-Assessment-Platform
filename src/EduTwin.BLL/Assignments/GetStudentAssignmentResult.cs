namespace EduTwin.BLL.Assignments;

public class GetStudentAssignmentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public EduTwin.Contracts.Assignments.StudentAssignmentDetailResponse? Data { get; }

    private GetStudentAssignmentResult(bool isSuccess, string? errorCode, EduTwin.Contracts.Assignments.StudentAssignmentDetailResponse? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static GetStudentAssignmentResult Success(EduTwin.Contracts.Assignments.StudentAssignmentDetailResponse data) => new(true, null, data);
    public static GetStudentAssignmentResult Failure(string errorCode) => new(false, errorCode, null);
}
