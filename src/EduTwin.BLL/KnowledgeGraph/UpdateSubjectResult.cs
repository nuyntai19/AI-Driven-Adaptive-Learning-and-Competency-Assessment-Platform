using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class UpdateSubjectResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public SubjectDto? Data { get; }
    public string? ErrorMessage { get; }
    public int? ActiveClassCount { get; }

    private UpdateSubjectResult(bool isSuccess, string? errorCode, SubjectDto? data, string? errorMessage = null, int? activeClassCount = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        ErrorMessage = errorMessage;
        ActiveClassCount = activeClassCount;
    }

    public static UpdateSubjectResult Success(SubjectDto data) => new(true, null, data);
    public static UpdateSubjectResult Failure(string errorCode, string? errorMessage = null, int? activeClassCount = null)
        => new(false, errorCode, null, errorMessage, activeClassCount);
}
