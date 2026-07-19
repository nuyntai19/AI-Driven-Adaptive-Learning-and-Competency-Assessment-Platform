namespace EduTwin.BLL.Organization;

public class DeleteTeacherResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }

    private DeleteTeacherResult(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static DeleteTeacherResult Success()
    {
        return new DeleteTeacherResult(true, null);
    }

    public static DeleteTeacherResult Failure(string errorCode)
    {
        return new DeleteTeacherResult(false, errorCode);
    }
}
