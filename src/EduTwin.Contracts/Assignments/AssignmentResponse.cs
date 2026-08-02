using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Assignments;

/// <summary>
/// Response envelope cho single Assignment resource.
/// </summary>
public class AssignmentResponse
{
    public AssignmentDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
