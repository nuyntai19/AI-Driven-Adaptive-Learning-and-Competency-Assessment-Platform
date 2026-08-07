namespace EduTwin.BLL.Assignments;

public class ListStudentAssignmentsResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public EduTwin.Contracts.Assignments.StudentAssignmentListResponse? Data { get; }

    private ListStudentAssignmentsResult(bool isSuccess, string? errorCode, EduTwin.Contracts.Assignments.StudentAssignmentListResponse? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static ListStudentAssignmentsResult Success(EduTwin.Contracts.Assignments.StudentAssignmentListResponse data) => new(true, null, data);
    public static ListStudentAssignmentsResult Failure(string errorCode) => new(false, errorCode, null);
}
