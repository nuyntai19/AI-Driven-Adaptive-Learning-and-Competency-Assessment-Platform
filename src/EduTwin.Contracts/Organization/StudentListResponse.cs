using System.Collections.Generic;
using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.Organization;

public class StudentListResponse
{
    public IReadOnlyList<StudentDto> Data { get; set; } = new List<StudentDto>();
    public PagedMetaDto Meta { get; set; } = new PagedMetaDto();
}
