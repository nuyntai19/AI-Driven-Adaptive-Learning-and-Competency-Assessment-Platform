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

    public static AttemptFeedbackResult Success(AttemptFeedbackDataDto data) =>
        new() { IsSuccess = true, Data = data };

    public static AttemptFeedbackResult ValidationFailed() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ValidationFailed };

    public static AttemptFeedbackResult NotFound() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ResourceNotFound };

    public static AttemptFeedbackResult Forbidden() =>
        new() { IsSuccess = false, ErrorCode = ErrorCodes.ForbiddenResource };
}
