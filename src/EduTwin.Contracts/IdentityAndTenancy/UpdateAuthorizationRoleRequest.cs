using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.IdentityAndTenancy;

public sealed class UpdateAuthorizationRoleRequest
{
    [Required]
    [StringLength(150)]
    public string RoleName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [EnumDataType(typeof(AuthorizationRoleStatus))]
    public AuthorizationRoleStatus? Status { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
