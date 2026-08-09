using System.Collections.Generic;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public class GetAssignmentProgressResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public IReadOnlyList<AssignmentProgressItemDto>? Data { get; }

    private GetAssignmentProgressResult(
        bool isSuccess,
        string? errorCode,
        IReadOnlyList<AssignmentProgressItemDto>? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static GetAssignmentProgressResult Success(IReadOnlyList<AssignmentProgressItemDto> data) =>
        new(true, null, data);

    public static GetAssignmentProgressResult Failure(string errorCode) =>
        new(false, errorCode, null);
}
