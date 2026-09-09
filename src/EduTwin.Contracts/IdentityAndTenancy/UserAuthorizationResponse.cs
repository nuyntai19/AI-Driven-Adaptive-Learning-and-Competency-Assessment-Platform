using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class UserAuthorizationResponse
{
    public required UserAuthorizationDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}
