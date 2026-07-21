using System.Collections.Generic;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.DigitalTwin;

public class ListStudentSubjectGoalsResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public IReadOnlyList<StudentSubjectGoalDto>? Data { get; }

    private ListStudentSubjectGoalsResult(bool isSuccess, string? errorCode, IReadOnlyList<StudentSubjectGoalDto>? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static ListStudentSubjectGoalsResult Success(IReadOnlyList<StudentSubjectGoalDto> data) => new(true, null, data);
    public static ListStudentSubjectGoalsResult Failure(string errorCode) => new(false, errorCode, null);
}
