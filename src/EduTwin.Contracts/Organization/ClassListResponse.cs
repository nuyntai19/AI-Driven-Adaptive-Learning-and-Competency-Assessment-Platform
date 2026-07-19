using System.Collections.Generic;
using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.Organization;

public class ClassListResponse
{
    public IReadOnlyList<ClassDto> Data { get; set; } = new List<ClassDto>();
    public PagedMetaDto Meta { get; set; } = new PagedMetaDto();
}
