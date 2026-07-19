using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class StudentDetailResponse
{
    public required StudentDetailDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}
