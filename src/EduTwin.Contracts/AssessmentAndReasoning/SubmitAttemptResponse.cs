using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class SubmitAttemptResponse
{
    public required SubmitAttemptAcceptedDataDto Data { get; init; }
    public required MetaDto Meta { get; init; }
}
