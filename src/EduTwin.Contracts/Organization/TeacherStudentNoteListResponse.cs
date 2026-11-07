using System.Collections.Generic;
using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.Organization;

public class TeacherStudentNoteListResponse
{
    public required IReadOnlyList<TeacherStudentNoteDto> Data { get; set; }
    public required PagedMetaDto Meta { get; set; }
}
