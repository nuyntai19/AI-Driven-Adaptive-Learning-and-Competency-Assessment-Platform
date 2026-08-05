using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public class CloseAssignmentResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public AssignmentDto? Data { get; private set; }

    public static CloseAssignmentResult Success(AssignmentDto data)
    {
        return new CloseAssignmentResult { IsSuccess = true, Data = data };
    }

    public static CloseAssignmentResult Failure(string errorCode)
    {
        return new CloseAssignmentResult { IsSuccess = false, ErrorCode = errorCode };
    }
}
