using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class CreateCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }

    private CreateCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static CreateCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static CreateCurriculumResult Failure(string errorCode) => new(false, errorCode, null);
}
