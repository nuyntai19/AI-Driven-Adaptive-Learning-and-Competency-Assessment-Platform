using EduTwin.Contracts.Common;

namespace EduTwin.BLL.Organization;

public class DeleteTeacherStudentNoteResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    private DeleteTeacherStudentNoteResult(bool isSuccess, string? errorCode, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static DeleteTeacherStudentNoteResult Success() => new(true, null);

    public static DeleteTeacherStudentNoteResult ValidationFailed(string? message = null) =>
        new(false, ErrorCodes.ValidationFailed, message);

    public static DeleteTeacherStudentNoteResult NotFound(string? message = null) =>
        new(false, ErrorCodes.ResourceNotFound, message);

    public static DeleteTeacherStudentNoteResult Forbidden(string? message = null) =>
        new(false, ErrorCodes.ForbiddenResource, message);

    public static DeleteTeacherStudentNoteResult ConcurrencyConflict(string? message = null) =>
        new(false, ErrorCodes.ConcurrencyConflict, message);
}
