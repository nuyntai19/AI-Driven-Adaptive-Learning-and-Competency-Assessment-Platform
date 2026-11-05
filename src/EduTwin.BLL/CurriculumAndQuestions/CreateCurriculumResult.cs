using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class CreateCurriculumResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }
    public CurriculumScopeConflictDto? ScopeConflict { get; }

    private CreateCurriculumResult(bool isSuccess, string? errorCode, CurriculumDto? data, CurriculumScopeConflictDto? scopeConflict = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        ScopeConflict = scopeConflict;
    }

    public static CreateCurriculumResult Success(CurriculumDto data) => new(true, null, data);
    public static CreateCurriculumResult Failure(string errorCode, CurriculumScopeConflictDto? scopeConflict = null) => new(false, errorCode, null, scopeConflict);
}
