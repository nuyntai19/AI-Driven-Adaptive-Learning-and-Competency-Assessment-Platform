using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class AssignCurriculumClassesResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }

    private AssignCurriculumClassesResult(bool isSuccess, string? errorCode, CurriculumDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static AssignCurriculumClassesResult Success(CurriculumDto data) => new(true, null, data);
    public static AssignCurriculumClassesResult Failure(string errorCode) => new(false, errorCode, null);
}
