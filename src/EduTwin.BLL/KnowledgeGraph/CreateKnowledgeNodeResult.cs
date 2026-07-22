using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class CreateKnowledgeNodeResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public KnowledgeNodeDto? Data { get; }

    private CreateKnowledgeNodeResult(bool isSuccess, string? errorCode, KnowledgeNodeDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static CreateKnowledgeNodeResult Success(KnowledgeNodeDto data) => new(true, null, data);
    public static CreateKnowledgeNodeResult Failure(string errorCode) => new(false, errorCode, null);
}
