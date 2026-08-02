namespace EduTwin.BLL.Assignments;

public class CreateAssignmentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public EduTwin.Contracts.Assignments.AssignmentDto? Data { get; }

    private CreateAssignmentResult(bool isSuccess, string? errorCode, EduTwin.Contracts.Assignments.AssignmentDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static CreateAssignmentResult Success(EduTwin.Contracts.Assignments.AssignmentDto data) => new(true, null, data);
    public static CreateAssignmentResult Failure(string errorCode) => new(false, errorCode, null);
}
