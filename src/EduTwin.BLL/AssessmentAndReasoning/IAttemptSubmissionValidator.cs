using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning;

public interface IAttemptSubmissionValidator
{
    Task<AttemptSubmissionValidationResult> ValidateAsync(
        SubmitAttemptRequest request,
        CancellationToken cancellationToken = default);
}
