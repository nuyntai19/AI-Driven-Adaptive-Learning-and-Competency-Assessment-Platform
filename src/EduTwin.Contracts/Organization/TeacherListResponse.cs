using System.Collections.Generic;
using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.Organization;

public class TeacherListResponse
{
    public IReadOnlyList<TeacherDto> Data { get; set; } = new List<TeacherDto>();
    public PagedMetaDto Meta { get; set; } = new PagedMetaDto();
}
