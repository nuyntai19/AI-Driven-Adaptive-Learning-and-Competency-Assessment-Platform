using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.AI;

public sealed record AnalyzeReasoningRequest
{
    public required string SchemaVersion { get; init; }

    public required string Language { get; init; }

    public required AnalyzeReasoningQuestion Question { get; init; }

    public required AnalyzeReasoningStudentSubmission StudentSubmission { get; init; }

    public required IReadOnlyList<AnalyzeReasoningAllowedKnowledgeNode> AllowedKnowledgeNodes { get; init; }
}

public sealed record AnalyzeReasoningQuestion
{
    public QuestionType QuestionType { get; init; }

    public required string QuestionText { get; init; }

    public required string CorrectAnswer { get; init; }

    public required string Solution { get; init; }

    public string? ExpectedReasoning { get; init; }

    public required AnalyzeReasoningGradingCriteria GradingCriteria { get; init; }
}

public sealed record AnalyzeReasoningGradingCriteria
{
    public required string SchemaVersion { get; init; }

    public required IReadOnlyList<string> RequiredIdeas { get; init; }

    public required IReadOnlyList<string> CommonErrors { get; init; }

    public required string ScoringNotes { get; init; }
}

public sealed record AnalyzeReasoningStudentSubmission
{
    public required string FinalAnswer { get; init; }

    public string? ReasoningText { get; init; }

    public uint TimeSpentSeconds { get; init; }

    public decimal Confidence { get; init; }

    public uint AnswerChanges { get; init; }

    public IReadOnlyList<AnalyzeReasoningImagePart> ImageParts { get; init; } = [];
}

public sealed record AnalyzeReasoningImagePart(byte[] Data, string MimeType);

public sealed record AnalyzeReasoningAllowedKnowledgeNode
{
    public required string NodeId { get; init; }

    public required string NodeName { get; init; }
}
