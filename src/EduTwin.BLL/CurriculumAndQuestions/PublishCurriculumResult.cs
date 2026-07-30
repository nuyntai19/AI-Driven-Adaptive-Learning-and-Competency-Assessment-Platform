using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class PublishCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }

    private PublishCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static PublishCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static PublishCurriculumResult Failure(string errorCode) => new(false, errorCode, null);
}
