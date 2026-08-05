using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

/// <summary>Result wrapper cho PublishAssignmentUseCase.</summary>
public class PublishAssignmentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public AssignmentDto? Data { get; }

    private PublishAssignmentResult(bool isSuccess, string? errorCode, AssignmentDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static PublishAssignmentResult Success(AssignmentDto data) => new(true, null, data);
    public static PublishAssignmentResult Failure(string errorCode) => new(false, errorCode, null);
}
