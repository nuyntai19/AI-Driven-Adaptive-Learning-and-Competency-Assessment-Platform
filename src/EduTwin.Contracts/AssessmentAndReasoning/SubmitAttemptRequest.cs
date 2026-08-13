namespace EduTwin.Contracts.AssessmentAndReasoning;

/// <summary>
/// Request body for POST /api/v1/learning/attempts (API_CONTRACTS.md section 52).
/// Tenant and student identity are always resolved from the authenticated context.
/// </summary>
public class SubmitAttemptRequest
{
    public Guid ClientSubmissionId { get; set; }
    public string QuestionId { get; set; } = string.Empty;
    public Guid? AssignmentId { get; set; }
    public string FinalAnswer { get; set; } = string.Empty;
    public string? ReasoningText { get; set; }
    public uint TimeSpentSeconds { get; set; }
    public decimal Confidence { get; set; }
    public uint AnswerChanges { get; set; }
    public bool Skipped { get; set; }
}
