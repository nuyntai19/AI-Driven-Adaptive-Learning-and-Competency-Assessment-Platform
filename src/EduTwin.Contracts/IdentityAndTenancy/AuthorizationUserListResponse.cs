using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationUserListResponse
{
    public IReadOnlyList<AuthorizationUserDto> Data { get; set; } = [];
    public PagedMetaDto Meta { get; set; } = new();
}
