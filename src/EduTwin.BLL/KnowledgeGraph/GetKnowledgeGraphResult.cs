using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class GetKnowledgeGraphResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public KnowledgeGraphDto? Data { get; }

    private GetKnowledgeGraphResult(bool isSuccess, string? errorCode, KnowledgeGraphDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static GetKnowledgeGraphResult Success(KnowledgeGraphDto data) => new(true, null, data);
    public static GetKnowledgeGraphResult Failure(string errorCode) => new(false, errorCode, null);
}
