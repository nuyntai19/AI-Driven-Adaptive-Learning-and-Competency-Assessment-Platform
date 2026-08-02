namespace EduTwin.BLL.Assignments;

public class GetAssignmentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public EduTwin.Contracts.Assignments.AssignmentDto? Data { get; }

    private GetAssignmentResult(bool isSuccess, string? errorCode, EduTwin.Contracts.Assignments.AssignmentDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static GetAssignmentResult Success(EduTwin.Contracts.Assignments.AssignmentDto data) => new(true, null, data);
    public static GetAssignmentResult Failure(string errorCode) => new(false, errorCode, null);
}
