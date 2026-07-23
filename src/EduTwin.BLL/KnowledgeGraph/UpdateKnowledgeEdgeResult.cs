using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class UpdateKnowledgeEdgeResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public KnowledgeEdgeDto? Data { get; private set; }

    public static UpdateKnowledgeEdgeResult Success(KnowledgeEdgeDto data)
    {
        return new UpdateKnowledgeEdgeResult
        {
            IsSuccess = true,
            Data = data
        };
    }

    public static UpdateKnowledgeEdgeResult Failure(string errorCode)
    {
        return new UpdateKnowledgeEdgeResult
        {
            IsSuccess = false,
            ErrorCode = errorCode
        };
    }
}
