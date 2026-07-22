namespace EduTwin.BLL.KnowledgeGraph;

public class DeleteSubjectResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }

    private DeleteSubjectResult(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static DeleteSubjectResult Success() =>
        new(true, null);

    public static DeleteSubjectResult Failure(string errorCode) =>
        new(false, errorCode);
}
