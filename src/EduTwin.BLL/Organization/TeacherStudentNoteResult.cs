using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class TeacherStudentNoteResult
{
    public bool IsSuccess { get; private set; }
    public TeacherStudentNoteDto? Data { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    private TeacherStudentNoteResult(bool isSuccess, TeacherStudentNoteDto? data, string? errorCode, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static TeacherStudentNoteResult Success(TeacherStudentNoteDto data) =>
        new(true, data, null);

    public static TeacherStudentNoteResult ValidationFailed(string? message = null) =>
        new(false, null, ErrorCodes.ValidationFailed, message);

    public static TeacherStudentNoteResult NotFound(string? message = null) =>
        new(false, null, ErrorCodes.ResourceNotFound, message);

    public static TeacherStudentNoteResult Forbidden(string? message = null) =>
        new(false, null, ErrorCodes.ForbiddenResource, message);

    public static TeacherStudentNoteResult ConcurrencyConflict(string? message = null) =>
        new(false, null, ErrorCodes.ConcurrencyConflict, message);
}
