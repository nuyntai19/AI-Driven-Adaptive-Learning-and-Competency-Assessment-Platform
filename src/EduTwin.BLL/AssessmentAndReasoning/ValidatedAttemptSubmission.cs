namespace EduTwin.BLL.AssessmentAndReasoning;

/// <summary>
/// Tenant-safe, graded submission prepared for the P11-T02 persistence transaction.
/// This is a BLL value and never crosses the public API boundary directly.
/// </summary>
public sealed class ValidatedAttemptSubmission
{
    public Guid CenterId { get; init; }
    public Guid StudentId { get; init; }
    public Guid ClientSubmissionId { get; init; }
    public ulong QuestionId { get; init; }
    public Guid? AssignmentId { get; init; }
    public string FinalAnswer { get; init; } = string.Empty;
    public string? ReasoningText { get; init; }
    public uint TimeSpentSeconds { get; init; }
    public decimal Confidence { get; init; }
    public uint AnswerChanges { get; init; }
    public bool Skipped { get; init; }
    public string ReasoningLanguage { get; init; } = string.Empty;
    public bool? IsCorrect { get; init; }
    public decimal? AwardedScore { get; init; }
    public string? AnswerDisplayLatex { get; init; }

    /// <summary>
    /// Set when this is an idempotent replay of an already-persisted submission.
    /// </summary>
    public ulong? ExistingAttemptId { get; init; }

    public bool IsIdempotentReplay => ExistingAttemptId.HasValue;
}
