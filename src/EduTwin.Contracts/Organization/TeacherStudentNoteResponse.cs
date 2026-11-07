using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class TeacherStudentNoteResponse
{
    public required TeacherStudentNoteDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}
