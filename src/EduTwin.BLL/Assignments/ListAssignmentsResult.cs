using System.Collections.Generic;

namespace EduTwin.BLL.Assignments;

public class ListAssignmentsResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public List<EduTwin.Contracts.Assignments.AssignmentDto>? Data { get; }
    public long TotalItems { get; }

    private ListAssignmentsResult(bool isSuccess, string? errorCode, List<EduTwin.Contracts.Assignments.AssignmentDto>? data, long totalItems)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        TotalItems = totalItems;
    }

    public static ListAssignmentsResult Success(List<EduTwin.Contracts.Assignments.AssignmentDto> data, long totalItems) =>
        new(true, null, data, totalItems);

    public static ListAssignmentsResult Failure(string errorCode) => new(false, errorCode, null, 0);
}
