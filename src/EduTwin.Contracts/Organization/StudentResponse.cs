using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class StudentResponse
{
    public StudentDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
