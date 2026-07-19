using System;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class StudentDto
{
    public Guid StudentId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public byte GradeLevel { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ActiveClassCount { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
