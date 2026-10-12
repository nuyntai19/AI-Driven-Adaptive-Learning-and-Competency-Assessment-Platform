using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class UpdateKnowledgeNodeResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public KnowledgeNodeDto? Data { get; }
    public string? Message { get; private set; }

    private UpdateKnowledgeNodeResult(bool isSuccess, string? errorCode, KnowledgeNodeDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpdateKnowledgeNodeResult Success(KnowledgeNodeDto data) => new(true, null, data);
    public static UpdateKnowledgeNodeResult Failure(string errorCode, string? message = null) => new(false, errorCode, null) { Message = message };
}
