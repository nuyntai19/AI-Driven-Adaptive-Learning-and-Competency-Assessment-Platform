using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class StudentSubjectGoalListResponse
{
    public required IReadOnlyList<StudentSubjectGoalDto> Data { get; set; }
    public required MetaDto Meta { get; set; }
}
