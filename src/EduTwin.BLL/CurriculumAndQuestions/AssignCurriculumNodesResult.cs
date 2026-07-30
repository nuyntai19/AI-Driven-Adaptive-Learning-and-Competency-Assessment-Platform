using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public class AssignCurriculumNodesResult
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public CurriculumDto? Data { get; }

    private AssignCurriculumNodesResult(bool isSuccess, string? errorCode, CurriculumDto? data)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        Data = data;
    }

    public static AssignCurriculumNodesResult Success(CurriculumDto data) => new(true, null, data);
    public static AssignCurriculumNodesResult Failure(string errorCode) => new(false, errorCode, null);
}
