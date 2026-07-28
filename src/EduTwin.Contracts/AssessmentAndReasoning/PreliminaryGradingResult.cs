namespace EduTwin.Contracts.AssessmentAndReasoning;

public class PreliminaryGradingResult
{
    /// <summary>
    /// Indicates whether the student's answer is correct. 
    /// For Essay questions, this may be null indicating it requires teacher or AI review.
    /// </summary>
    public bool? IsCorrect { get; set; }

    /// <summary>
    /// The score awarded for the answer.
    /// </summary>
    public decimal Score { get; set; }

    /// <summary>
    /// Immediate feedback or reason for the awarded score.
    /// </summary>
    public string? Feedback { get; set; }
}
