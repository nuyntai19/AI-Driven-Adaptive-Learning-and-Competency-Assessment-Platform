using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class CreateQuestionResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public QuestionDto? Data { get; }

    private CreateQuestionResult(bool isSuccess, string? errorCode, QuestionDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static CreateQuestionResult Success(QuestionDto data) => new(true, null, data);
    public static CreateQuestionResult Failure(string errorCode) => new(false, errorCode, null);
}
