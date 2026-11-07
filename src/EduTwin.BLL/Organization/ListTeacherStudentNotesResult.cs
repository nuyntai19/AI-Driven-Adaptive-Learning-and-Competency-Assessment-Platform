using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class ListTeacherStudentNotesResult
{
    public bool IsSuccess { get; private set; }
    public IReadOnlyList<TeacherStudentNoteDto>? Data { get; private set; }
    public int TotalItems { get; private set; }
    public int TotalPages { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    private ListTeacherStudentNotesResult(bool isSuccess, IReadOnlyList<TeacherStudentNoteDto>? data, int totalItems, int totalPages, string? errorCode, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        Data = data;
        TotalItems = totalItems;
        TotalPages = totalPages;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static ListTeacherStudentNotesResult Success(IReadOnlyList<TeacherStudentNoteDto> data, int totalItems, int totalPages) =>
        new(true, data, totalItems, totalPages, null);

    public static ListTeacherStudentNotesResult ValidationFailed(string? message = null) =>
        new(false, null, 0, 0, ErrorCodes.ValidationFailed, message);

    public static ListTeacherStudentNotesResult Forbidden(string? message = null) =>
        new(false, null, 0, 0, ErrorCodes.ForbiddenResource, message);

    public static ListTeacherStudentNotesResult NotFound(string? message = null) =>
        new(false, null, 0, 0, ErrorCodes.ResourceNotFound, message);
}
