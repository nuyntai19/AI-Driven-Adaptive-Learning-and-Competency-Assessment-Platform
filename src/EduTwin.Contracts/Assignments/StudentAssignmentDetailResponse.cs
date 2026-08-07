using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Assignments;

public class StudentAssignmentDetailResponse
{
    public StudentAssignmentDetailDto Data { get; set; } = new();
    public MetaDto Meta { get; set; } = null!;
}
