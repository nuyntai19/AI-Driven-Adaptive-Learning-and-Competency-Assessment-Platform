namespace EduTwin.BLL.KnowledgeGraph;

public class DeleteKnowledgeEdgeResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }

    private DeleteKnowledgeEdgeResult(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static DeleteKnowledgeEdgeResult Success() => new(true, null);

    public static DeleteKnowledgeEdgeResult Failure(string errorCode) => new(false, errorCode);
}
