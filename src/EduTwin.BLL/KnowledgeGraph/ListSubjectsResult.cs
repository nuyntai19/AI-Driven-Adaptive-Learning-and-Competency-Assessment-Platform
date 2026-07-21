using System.Collections.Generic;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class ListSubjectsResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public IReadOnlyList<SubjectDto>? Data { get; }

    private ListSubjectsResult(bool isSuccess, string? errorCode, IReadOnlyList<SubjectDto>? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static ListSubjectsResult Success(IReadOnlyList<SubjectDto> data)
        => new ListSubjectsResult(true, null, data);

    public static ListSubjectsResult Failure(string errorCode)
        => new ListSubjectsResult(false, errorCode, null);
}
