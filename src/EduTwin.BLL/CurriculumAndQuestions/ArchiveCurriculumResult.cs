using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class ArchiveCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }
    public string? Message { get; private set; }

    private ArchiveCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static ArchiveCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static ArchiveCurriculumResult Failure(string errorCode, string? message = null) => new(false, errorCode, null) { Message = message };
}
