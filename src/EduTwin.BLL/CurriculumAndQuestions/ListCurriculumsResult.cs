using System.Collections.Generic;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class ListCurriculumsResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorCode { get; private set; }
    public List<CurriculumDto>? Data { get; private set; }

    public static ListCurriculumsResult Success(List<CurriculumDto> data)
    {
        return new ListCurriculumsResult
        {
            IsSuccess = true,
            Data = data
        };
    }

    public static ListCurriculumsResult Failure(string errorCode)
    {
        return new ListCurriculumsResult
        {
            IsSuccess = false,
            ErrorCode = errorCode
        };
    }
}
