using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class GetQuestionResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public QuestionDto? Data { get; }

    private GetQuestionResult(bool isSuccess, string? errorCode, QuestionDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static GetQuestionResult Success(QuestionDto data) => new(true, null, data);
    public static GetQuestionResult Failure(string errorCode) => new(false, errorCode, null);
}
