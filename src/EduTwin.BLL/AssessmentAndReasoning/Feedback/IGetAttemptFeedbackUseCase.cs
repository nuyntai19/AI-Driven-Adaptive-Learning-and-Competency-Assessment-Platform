using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;

namespace EduTwin.BLL.AssessmentAndReasoning.Feedback;

public interface IGetAttemptFeedbackUseCase
{
    Task<AttemptFeedbackResult> ExecuteAsync(ulong attemptId, CancellationToken cancellationToken);
}

public sealed class AttemptFeedbackResult
{
    public bool IsSuccess { get; private init; }
    public AttemptFeedbackDataDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static AttemptFeedbackResult Success(AttemptFeedbackDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static AttemptFeedbackResult ValidationFailed(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed, ErrorMessage = message };

    public static AttemptFeedbackResult NotFound(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound, ErrorMessage = message };

    public static AttemptFeedbackResult Forbidden(string? message = null) =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource, ErrorMessage = message };
}
