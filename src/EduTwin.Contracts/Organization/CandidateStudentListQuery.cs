namespace EduTwin.Contracts.Organization;

public class CandidateStudentListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}
