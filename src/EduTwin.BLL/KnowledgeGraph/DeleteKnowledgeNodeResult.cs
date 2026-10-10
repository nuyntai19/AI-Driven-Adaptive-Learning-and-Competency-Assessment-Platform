namespace EduTwin.BLL.KnowledgeGraph;

public class DeleteKnowledgeNodeResult
{
    public bool IsSuccess { get; }
    public string ErrorCode { get; }
    public string? Message { get; private set; }

    private DeleteKnowledgeNodeResult(bool isSuccess, string errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static DeleteKnowledgeNodeResult Success() => new(true, string.Empty);
    public static DeleteKnowledgeNodeResult Failure(string errorCode, string? message = null) => new(false, errorCode) { Message = message };
}
