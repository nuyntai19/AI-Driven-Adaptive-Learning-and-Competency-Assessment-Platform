using System;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class StudentListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public UserStatus? Status { get; set; }
    public byte? GradeLevel { get; set; }
    public Guid? ClassId { get; set; }
}
