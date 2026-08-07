using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Assignments;

public class StudentAssignmentListResponse
{
    public IReadOnlyList<StudentAssignmentDto> Data { get; set; } = new List<StudentAssignmentDto>();
    public PagedMetaDto Meta { get; set; } = new();
}
