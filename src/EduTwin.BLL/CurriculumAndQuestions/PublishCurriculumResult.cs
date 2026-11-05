using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class PublishCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }
    public CurriculumScopeConflictDto? ScopeConflict { get; }

    private PublishCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data, CurriculumScopeConflictDto? scopeConflict = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        ScopeConflict = scopeConflict;
    }

    public static PublishCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static PublishCurriculumResult Failure(string errorCode, CurriculumScopeConflictDto? scopeConflict = null) => new(false, errorCode, null, scopeConflict);
}
