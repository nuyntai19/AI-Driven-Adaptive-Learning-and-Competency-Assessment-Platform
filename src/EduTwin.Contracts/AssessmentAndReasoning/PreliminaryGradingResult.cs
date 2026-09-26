namespace EduTwin.Contracts.AssessmentAndReasoning;

public class PreliminaryGradingResult
{
    /// <summary>
    /// Indicates whether the student's answer is correct. 
    /// For Essay questions, this remains null until a teacher provides a human-confirmed grade.
    /// </summary>
    public bool? IsCorrect { get; set; }

    /// <summary>
    /// The score awarded for the answer.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Stable machine-readable explanation for the preliminary grading decision.
    /// </summary>
    public string? ReasonCode { get; set; }

    /// <summary>
    /// Immediate feedback or reason for the awarded score.
    /// </summary>
    public string? Feedback { get; set; }
}
