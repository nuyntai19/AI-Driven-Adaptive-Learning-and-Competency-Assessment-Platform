using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class ArchiveQuestionResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public QuestionDto? Data { get; }

    private ArchiveQuestionResult(bool isSuccess, string? errorCode, QuestionDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static ArchiveQuestionResult Success(QuestionDto data) => new(true, null, data);
    public static ArchiveQuestionResult Failure(string errorCode) => new(false, errorCode, null);
}
