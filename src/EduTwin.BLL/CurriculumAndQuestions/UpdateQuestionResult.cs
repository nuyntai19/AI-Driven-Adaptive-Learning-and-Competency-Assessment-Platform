using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class UpdateQuestionResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public QuestionDto? Data { get; }

    private UpdateQuestionResult(bool isSuccess, string? errorCode, QuestionDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static UpdateQuestionResult Success(QuestionDto data) => new(true, null, data);
    public static UpdateQuestionResult Failure(string errorCode) => new(false, errorCode, null);
}
