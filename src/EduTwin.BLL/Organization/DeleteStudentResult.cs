namespace EduTwin.BLL.Organization;

public class DeleteStudentResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }

    private DeleteStudentResult(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static DeleteStudentResult Success()
    {
        return new DeleteStudentResult(true, null);
    }

    public static DeleteStudentResult Failure(string errorCode)
    {
        return new DeleteStudentResult(false, errorCode);
    }
}
