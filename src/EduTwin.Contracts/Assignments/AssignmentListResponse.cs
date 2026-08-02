using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Assignments;

/// <summary>
/// Response envelope cho collection Assignment.
/// </summary>
public class AssignmentListResponse
{
    public List<AssignmentDto> Data { get; set; } = new();
    public PagedMetaDto Meta { get; set; } = new();
}
