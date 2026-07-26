namespace EduTwin.BLL.CurriculumAndQuestions;

public class DeleteQuestionResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }

    private DeleteQuestionResult(bool isSuccess, string? errorCode)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
    }

    public static DeleteQuestionResult Success() => new(true, null);
    public static DeleteQuestionResult Failure(string errorCode) => new(false, errorCode);
}
