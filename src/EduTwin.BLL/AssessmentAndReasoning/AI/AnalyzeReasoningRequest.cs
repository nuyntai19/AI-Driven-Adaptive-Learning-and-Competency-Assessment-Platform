using EduTwin.Contracts.CurriculumAndQuestions;
using System.Text.Json.Serialization;

namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed record AnalyzeReasoningRequest
{
    // Server-owned repair hint, not student data and not part of checkpoint identity.
    [JsonIgnore]
    public AIResponseValidationRule? ResponseRepairRule { get; init; }
    public required string SchemaVersion { get; init; }

    public required string Language { get; init; }

    public required AnalyzeReasoningQuestion Question { get; init; }

    public required AnalyzeReasoningStudentSubmission StudentSubmission { get; init; }

    public required IReadOnlyList<AnalyzeReasoningAllowedKnowledgeNode> AllowedKnowledgeNodes { get; init; }
}

public sealed record AnalyzeReasoningQuestion
{
    public decimal MaxScore { get; init; } = 10m;
    public string ContentLanguage { get; init; } = "vi";
    public IReadOnlyList<AnalyzeReasoningOption> Options { get; init; } = [];
    public QuestionType QuestionType { get; init; }

    public QuestionAnswerEvaluationMode AnswerEvaluationMode { get; init; } = QuestionAnswerEvaluationMode.TextExact;

    public required string QuestionText { get; init; }

    public required string CorrectAnswer { get; init; }

    public string? CanonicalCorrectAnswer { get; init; }

    public required string Solution { get; init; }

    public string? ExpectedReasoning { get; init; }

    public required AnalyzeReasoningGradingCriteria GradingCriteria { get; init; }
}

public sealed record AnalyzeReasoningGradingCriteria
{
    public IReadOnlyList<AnalyzeReasoningRubricCriterion> Criteria { get; init; } = [];
    public required string SchemaVersion { get; init; }

    public required IReadOnlyList<string> RequiredIdeas { get; init; }

    public required IReadOnlyList<string> CommonErrors { get; init; }

    public required string ScoringNotes { get; init; }
}

public sealed record AnalyzeReasoningOption(string Label, string Text)
{
    public AnalyzeReasoningOption() : this(string.Empty, string.Empty) { }
}
public sealed record AnalyzeReasoningRubricCriterion(string CriterionId, string Title, string Description, decimal MaxScore)
{
    public AnalyzeReasoningRubricCriterion() : this(string.Empty, string.Empty, string.Empty, 0m) { }
}

public sealed record AnalyzeReasoningStudentSubmission
{
    public required string FinalAnswer { get; init; }

    public string? AnswerDisplayLatex { get; init; }

    public string? CanonicalFinalAnswer { get; init; }

    /// <summary>
    /// Authoritative result from the deterministic preliminary grader. The AI
    /// explains the reasoning only and must not re-grade this value.
    /// </summary>
    public bool? PreliminaryIsCorrect { get; init; }

    public string? ReasoningText { get; init; }

    public uint TimeSpentSeconds { get; init; }

    public decimal Confidence { get; init; }

    public uint AnswerChanges { get; init; }

    [JsonIgnore] // Images are sent as multimodal parts, never duplicated as base64 prompt text.
    public IReadOnlyList<AnalyzeReasoningImagePart> ImageParts { get; init; } = [];
}

public sealed record AnalyzeReasoningImagePart(byte[] Data, string MimeType)
{
    public AnalyzeReasoningImagePart() : this([], "image/png") { }
}

public sealed record AnalyzeReasoningAllowedKnowledgeNode
{
    public required string NodeId { get; init; }

    public required string NodeName { get; init; }
}
