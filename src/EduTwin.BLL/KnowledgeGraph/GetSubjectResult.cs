using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class GetSubjectResult
{
    public bool IsSuccess { get; }
    public SubjectDto? Data { get; }
    public string? ErrorCode { get; }

    private GetSubjectResult(bool isSuccess, SubjectDto? data, string? errorCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
    }

    public static GetSubjectResult Success(SubjectDto data) => new(true, data, null);
    public static GetSubjectResult Failure(string errorCode) => new(false, null, errorCode);
}
