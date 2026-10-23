using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class AssignCurriculumNodesResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }
    public CurriculumScopeConflictDto? ScopeConflict { get; }

    private AssignCurriculumNodesResult(bool isSuccess, string? errorCode, CurriculumDto? data, CurriculumScopeConflictDto? scopeConflict = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
        ScopeConflict = scopeConflict;
    }

    public static AssignCurriculumNodesResult Success(CurriculumDto data) => new(true, null, data);
    public static AssignCurriculumNodesResult Failure(string errorCode, CurriculumScopeConflictDto? scopeConflict = null) => new(false, errorCode, null, scopeConflict);
}
