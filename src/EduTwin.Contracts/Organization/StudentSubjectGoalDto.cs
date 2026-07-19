namespace EduTwin.Contracts.Organization;

public class StudentSubjectGoalDto
{
    public required string GoalId { get; set; }
    public required string StudentId { get; set; }
    public required string SubjectId { get; set; }
    public decimal TargetScore { get; set; }
    public int RemainingDays { get; set; }
    public decimal CurrentPredictedScore { get; set; }
    public decimal RiskScore { get; set; }
    public required string RowVersion { get; set; }
}
