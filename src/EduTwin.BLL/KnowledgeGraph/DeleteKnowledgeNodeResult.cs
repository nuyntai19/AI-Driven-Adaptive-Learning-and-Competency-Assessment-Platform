namespace EduTwin.BLL.KnowledgeGraph;

public class DeleteKnowledgeNodeResult
{
    public bool IsSuccess { get; }
    public string ErrorCode { get; }

    private DeleteKnowledgeNodeResult(bool isSuccess, string errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static DeleteKnowledgeNodeResult Success() => new(true, string.Empty);
    public static DeleteKnowledgeNodeResult Failure(string errorCode) => new(false, errorCode);
}
