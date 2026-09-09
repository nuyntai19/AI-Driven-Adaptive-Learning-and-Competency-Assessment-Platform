using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class PermissionListResponse
{
    public IReadOnlyList<PermissionDto> Data { get; set; } = [];
    public PagedMetaDto Meta { get; set; } = new();
}
