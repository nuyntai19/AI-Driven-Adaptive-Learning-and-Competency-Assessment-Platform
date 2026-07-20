using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.DigitalTwin;

public class UpsertStudentSubjectGoalResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public StudentSubjectGoalDto? Data { get; }

    private UpsertStudentSubjectGoalResult(bool isSuccess, string? errorCode, StudentSubjectGoalDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpsertStudentSubjectGoalResult Success(StudentSubjectGoalDto data) => new(true, null, data);
    public static UpsertStudentSubjectGoalResult ValidationFailed() => new(false, EduTwin.Contracts.Common.ErrorCodes.ValidationFailed, null);
    public static UpsertStudentSubjectGoalResult Forbidden() => new(false, EduTwin.Contracts.Common.ErrorCodes.ForbiddenResource, null);
    public static UpsertStudentSubjectGoalResult NotFound() => new(false, EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, null);
    public static UpsertStudentSubjectGoalResult Conflict() => new(false, EduTwin.Contracts.Common.ErrorCodes.ConcurrencyConflict, null);
    public static UpsertStudentSubjectGoalResult Failure(string errorCode) => new(false, errorCode, null);
}
