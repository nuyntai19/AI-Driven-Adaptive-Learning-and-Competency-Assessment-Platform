using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationRoleResponse
{
    public required AuthorizationRoleDto Data { get; set; }
    public required MetaDto Meta { get; set; }
}

public sealed class AuthorizationRoleListResponse
{
    public required IReadOnlyList<AuthorizationRoleDto> Data { get; set; }
    public required PagedMetaDto Meta { get; set; }
}
