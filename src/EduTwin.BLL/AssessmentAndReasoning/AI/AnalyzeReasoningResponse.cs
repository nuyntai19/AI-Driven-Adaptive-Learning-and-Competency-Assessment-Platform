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

    public string? SolutionType { get; init; }

    public string? AiSolution { get; init; }

    // Advisory observations, never final grades. Null supports legacy responses.
    public string? AnswerAssessment { get; init; }
    public string? ReasoningVerdict { get; init; }
    // Proposals only. They become authoritative solely through a teacher action.
    public decimal? SuggestedScore { get; init; }
    public bool UsesAlternativeMethod { get; init; }
    public IReadOnlyList<RubricScoreInput> SuggestedRubricScores { get; init; } = [];
    // Null identifies legacy responses; new provider schemas require an array.
    public IReadOnlyList<AIReasoningIssue>? ReasoningIssues { get; init; }
    // Null is legacy. New visual rubrics require one observation per authored requirement.
    public IReadOnlyList<RubricVisualEvidence>? VisualEvidence { get; init; }
    // Required only by the independently inspected visual profile. Legacy and ordinary
    // analyses remain readable without this additional audit field.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<AICriterionDeduction>? CriterionDeductions { get; init; }
}

public sealed record AICriterionDeduction(string CriterionId, string EvidenceKind, string UnmetRequirement, string Evidence)
{
    public AICriterionDeduction() : this(string.Empty, string.Empty, string.Empty, string.Empty) { }
}

public sealed record AIReasoningIssue(string Verdict, string StudentClaim, string Explanation)
{
    public AIReasoningIssue() : this(string.Empty, string.Empty, string.Empty) { }
}
