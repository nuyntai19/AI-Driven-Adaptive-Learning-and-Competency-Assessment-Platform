using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class CreateKnowledgeEdgeResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public KnowledgeEdgeDto? Data { get; }
    public string? Message { get; private set; }

    private CreateKnowledgeEdgeResult(bool isSuccess, string? errorCode, KnowledgeEdgeDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static CreateKnowledgeEdgeResult Success(KnowledgeEdgeDto data) => new(true, null, data);
    public static CreateKnowledgeEdgeResult Failure(string errorCode, string? message = null) => new(false, errorCode, null) { Message = message };
}
