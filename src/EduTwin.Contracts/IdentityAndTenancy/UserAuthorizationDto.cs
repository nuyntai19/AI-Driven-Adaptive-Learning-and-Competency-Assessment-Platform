namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class AssignedAuthorizationRoleDto
{
    public Guid RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string AssignmentStatus { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class UserAuthorizationDto
{
    public Guid UserId { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public IReadOnlyList<AssignedAuthorizationRoleDto> Roles { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public string RowVersion { get; set; } = string.Empty;
    public uint AuthorizationVersion { get; set; }
}
