namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class PermissionDto
{
    public string PermissionCode { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedAccountTypes { get; set; } = [];
    public bool IsSensitive { get; set; }
    public bool IsDelegable { get; set; }
    public string Status { get; set; } = string.Empty;
}
