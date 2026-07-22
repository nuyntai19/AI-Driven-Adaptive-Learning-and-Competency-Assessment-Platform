using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class UpdateSubjectResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public SubjectDto? Data { get; }

    private UpdateSubjectResult(bool isSuccess, string? errorCode, SubjectDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpdateSubjectResult Success(SubjectDto data) => new(true, null, data);
    public static UpdateSubjectResult Failure(string errorCode) => new(false, errorCode, null);
}
