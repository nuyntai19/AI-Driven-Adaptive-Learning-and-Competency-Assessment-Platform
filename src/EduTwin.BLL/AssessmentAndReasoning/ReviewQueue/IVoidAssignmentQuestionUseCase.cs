using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;

public enum VoidAssignmentQuestionStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Forbidden,
    Conflict
}

public sealed class VoidAssignmentQuestionResult
{
    public VoidAssignmentQuestionStatus Status { get; private init; }
    public bool IsSuccess => Status == VoidAssignmentQuestionStatus.Success;
    public VoidAssignmentQuestionResultDto? Data { get; private init; }
    public string ErrorCode { get; private init; } = string.Empty;
    public string ErrorMessage { get; private init; } = string.Empty;

    public static VoidAssignmentQuestionResult Success(VoidAssignmentQuestionResultDto data) => new()
    {
        Status = VoidAssignmentQuestionStatus.Success,
        Data = data
    };

    public static VoidAssignmentQuestionResult Fail(VoidAssignmentQuestionStatus status, string errorCode, string errorMessage) => new()
    {
        Status = status,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}

public interface IVoidAssignmentQuestionUseCase
{
    Task<VoidAssignmentQuestionResult> ExecuteAsync(
        Guid assignmentId,
        ulong questionId,
        VoidAssignmentQuestionRequest request,
        CancellationToken cancellationToken = default);
}
