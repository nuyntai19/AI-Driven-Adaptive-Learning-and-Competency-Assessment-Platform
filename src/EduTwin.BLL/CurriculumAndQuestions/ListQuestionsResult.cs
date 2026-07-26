using System.Collections.Generic;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class ListQuestionsResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public List<QuestionDto>? Data { get; }
    public long TotalItems { get; }

    private ListQuestionsResult(bool isSuccess, string? errorCode, List<QuestionDto>? data, long totalItems)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        TotalItems = totalItems;
    }

    public static ListQuestionsResult Success(List<QuestionDto> data, long totalItems) => new(true, null, data, totalItems);
    public static ListQuestionsResult Failure(string errorCode) => new(false, errorCode, null, 0);
}
