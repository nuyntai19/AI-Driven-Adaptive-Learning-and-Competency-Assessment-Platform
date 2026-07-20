using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class StudentSubjectGoalResponse
{
    public required StudentSubjectGoalDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}
