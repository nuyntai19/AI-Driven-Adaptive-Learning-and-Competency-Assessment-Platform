using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class ReplaceUserRolesRequest
{
    [Required]
    public IReadOnlyList<Guid> RoleIds { get; set; } = [];

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
