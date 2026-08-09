using System.Collections.Generic;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Assignments;

public class AssignmentProgressListResponse
{
    public IReadOnlyList<AssignmentProgressItemDto> Data { get; set; } = new List<AssignmentProgressItemDto>();
    public MetaDto Meta { get; set; } = null!;
}
