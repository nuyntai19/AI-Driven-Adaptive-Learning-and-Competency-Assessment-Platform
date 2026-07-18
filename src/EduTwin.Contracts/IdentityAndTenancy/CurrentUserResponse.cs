using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.IdentityAndTenancy;

public class CurrentUserResponse
{
    public CurrentUserDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public class CurrentUserDataDto
{
    public string UserId { get; set; } = string.Empty;
    public string CenterId { get; set; } = string.Empty;
    public string CenterName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
