using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class ClassSubjectDto
{
    public required string SubjectId { get; set; }
    public required string SubjectName { get; set; }
}

public class ClassTeacherDto
{
    public required string TeacherId { get; set; }
    public required string DisplayName { get; set; }
}

public class ClassDto
{
    public required string ClassId { get; set; }
    public required string ClassName { get; set; }
    public required string AcademicYear { get; set; }
    public required ClassSubjectDto Subject { get; set; }
    public required ClassTeacherDto Teacher { get; set; }
    public int StudentCount { get; set; }
    public required string Status { get; set; }
    public required string RowVersion { get; set; }
}
