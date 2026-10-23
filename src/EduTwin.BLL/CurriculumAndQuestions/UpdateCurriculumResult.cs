using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class UpdateCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }
    public CurriculumScopeConflictDto? ScopeConflict { get; }

    private UpdateCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data, CurriculumScopeConflictDto? scopeConflict = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        ScopeConflict = scopeConflict;
    }

    public static UpdateCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static UpdateCurriculumResult Failure(string errorCode, CurriculumScopeConflictDto? scopeConflict = null) => new(false, errorCode, null, scopeConflict);
}
