using EduTwin.Contracts.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed record AnalyzeReasoningResponse
{
    public required string SchemaVersion { get; init; }

    public required string Language { get; init; }

    public string? MethodDetected { get; init; }

    public int ReasoningQuality { get; init; }

    public ErrorType ErrorType { get; init; }

    public string? Misconception { get; init; }

    public required IReadOnlyList<string> MissingSteps { get; init; }

    public required IReadOnlyList<string> RootCauseNodeIds { get; init; }

    public int Confidence { get; init; }

    public required string Feedback { get; init; }
}
