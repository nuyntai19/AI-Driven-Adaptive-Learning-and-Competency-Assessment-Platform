using System;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class CenterProfileDataDto
{
    public required string CenterId { get; set; }
    public required string CenterCode { get; set; }
    public required string CenterName { get; set; }
    public required string Status { get; set; }
    public required string Timezone { get; set; }
    public required string RowVersion { get; set; }
}

public class CenterProfileResponse
{
    public required CenterProfileDataDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}
