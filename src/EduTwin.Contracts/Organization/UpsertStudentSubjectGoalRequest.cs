namespace EduTwin.Contracts.Organization;

public class UpsertStudentSubjectGoalRequest
{
    public decimal TargetScore { get; set; }
    public int RemainingDays { get; set; }
    public string? RowVersion { get; set; }
}
