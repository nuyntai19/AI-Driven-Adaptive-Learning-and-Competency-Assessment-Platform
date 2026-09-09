using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class ReplaceRolePermissionsRequest
{
    [Required]
    public IReadOnlyList<string> PermissionCodes { get; set; } = [];

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
