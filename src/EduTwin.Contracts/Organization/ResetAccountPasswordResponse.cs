using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class ResetAccountPasswordData
{
    public string TargetUserId { get; set; } = string.Empty;
    public string NewRowVersion { get; set; } = string.Empty;
}

public class ResetAccountPasswordResponse
{
    public ResetAccountPasswordData Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
