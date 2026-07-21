using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class CreateSubjectResult
{
    public bool IsSuccess { get; private set; }
    public SubjectDto? Data { get; private set; }
    public string? ErrorCode { get; private set; }

    public static CreateSubjectResult Success(SubjectDto data) => new() { IsSuccess = true, Data = data };
    public static CreateSubjectResult Failure(string errorCode) => new() { IsSuccess = false, ErrorCode = errorCode };
}
