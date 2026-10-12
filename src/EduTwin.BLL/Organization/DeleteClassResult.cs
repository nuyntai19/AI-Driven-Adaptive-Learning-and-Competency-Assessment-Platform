namespace EduTwin.BLL.Organization;

public class DeleteClassResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private DeleteClassResult(bool isSuccess, string? errorCode, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static DeleteClassResult Success()
    {
        return new DeleteClassResult(true, null);
    }

    public static DeleteClassResult Failure(string errorCode, string? errorMessage = null)
    {
        return new DeleteClassResult(false, errorCode, errorMessage);
    }
}
