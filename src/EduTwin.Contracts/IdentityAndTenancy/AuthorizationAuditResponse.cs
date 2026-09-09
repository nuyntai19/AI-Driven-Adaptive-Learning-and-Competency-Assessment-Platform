using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationAuditResponse
{
    public required IReadOnlyList<AuthorizationAuditDto> Data { get; set; }
    public required PagedMetaDto Meta { get; set; }
}
