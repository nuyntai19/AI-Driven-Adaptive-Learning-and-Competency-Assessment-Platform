using System.Collections.Generic;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public class ListTeachersResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorCode { get; set; }
    public IReadOnlyList<TeacherDto>? Data { get; set; }
    public long TotalItems { get; set; }
    public int TotalPages { get; set; }

    public static ListTeachersResult Success(IReadOnlyList<TeacherDto> data, long totalItems, int totalPages)
    {
        return new ListTeachersResult
        {
            IsSuccess = true,
            Data = data,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public static ListTeachersResult Failure(string errorCode)
    {
        return new ListTeachersResult
        {
            IsSuccess = false,
            ErrorCode = errorCode
        };
    }
}
