using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.ReviewRequest;

public interface ICreateStudentReviewRequestUseCase
{
    Task<CreateStudentReviewRequestResult> ExecuteAsync(
        ulong attemptId,
        CreateStudentReviewRequest request,
        CancellationToken cancellationToken);
}

public sealed class CreateStudentReviewRequestResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }
    public StudentReviewRequestDto? Data { get; private init; }

    public static CreateStudentReviewRequestResult Success(StudentReviewRequestDto data) =>
        new() { IsSuccess = true, Data = data };

    public static CreateStudentReviewRequestResult Failure(string errorCode, string errorMessage) =>
        new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    public static CreateStudentReviewRequestResult NotFound() =>
        Failure("NOT_FOUND", "Attempt not found.");

    public static CreateStudentReviewRequestResult Forbidden() =>
        Failure("FORBIDDEN", "You can only request review for your own attempts.");

    public static CreateStudentReviewRequestResult ValidationFailed(string message) =>
        Failure("VALIDATION_FAILED", message);
}
