namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AuthorizationRoleDto
{
    public Guid RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<string> PermissionCodes { get; set; } = [];
    public int ActiveUserCount { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
