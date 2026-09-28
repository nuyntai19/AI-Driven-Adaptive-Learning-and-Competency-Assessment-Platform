using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class CloneCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }

    private CloneCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static CloneCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static CloneCurriculumResult Failure(string errorCode) => new(false, errorCode, null);
}
