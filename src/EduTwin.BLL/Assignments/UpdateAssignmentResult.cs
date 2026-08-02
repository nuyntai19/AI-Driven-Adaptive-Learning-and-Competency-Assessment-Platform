namespace EduTwin.BLL.Assignments;

public class UpdateAssignmentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public EduTwin.Contracts.Assignments.AssignmentDto? Data { get; }

    private UpdateAssignmentResult(bool isSuccess, string? errorCode, EduTwin.Contracts.Assignments.AssignmentDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpdateAssignmentResult Success(EduTwin.Contracts.Assignments.AssignmentDto data) => new(true, null, data);
    public static UpdateAssignmentResult Failure(string errorCode) => new(false, errorCode, null);
}
