using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class ArchiveCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }

    private ArchiveCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static ArchiveCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static ArchiveCurriculumResult Failure(string errorCode) => new(false, errorCode, null);
}
