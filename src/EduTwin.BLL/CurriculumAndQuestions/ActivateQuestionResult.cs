using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class ActivateQuestionResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public QuestionDto? Data { get; }

    private ActivateQuestionResult(bool isSuccess, string? errorCode, QuestionDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static ActivateQuestionResult Success(QuestionDto data) => new(true, null, data);
    public static ActivateQuestionResult Failure(string errorCode) => new(false, errorCode, null);
}
